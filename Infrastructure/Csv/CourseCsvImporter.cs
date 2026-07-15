using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;
using Microsoft.VisualBasic.FileIO;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Infrastructure.Csv;

public sealed class CourseCsvImporter : ICourseCsvImporter
{
    private static readonly string[] FOUR_COLUMN_HEADER = new string[]
    {
        "CourseId",
        "Section",
        "Name",
        "TimeSlots",
    };

    private static readonly string[] FIVE_COLUMN_HEADER = new string[]
    {
        "CourseId",
        "Section",
        "Name",
        "TimeSlots",
        "Classroom",
    };

    private readonly CourseCsvRecordParser mRecordParser;

    public CourseCsvImporter()
    {
        mRecordParser = new CourseCsvRecordParser();
    }

    public CourseImportResult ImportCourses(CsvInputFilePath inputFilePath)
    {
        CourseCsvImportOptions options = CourseCsvImportOptions.CreateDefault();
        return ImportCourses(inputFilePath, options, CancellationToken.None);
    }

    public CourseImportResult ImportCourses(
        CsvInputFilePath inputFilePath,
        CancellationToken cancellationToken)
    {
        CourseCsvImportOptions options = CourseCsvImportOptions.CreateDefault();
        return ImportCourses(inputFilePath, options, cancellationToken);
    }

    public CourseImportResult ImportCourses(
        CsvInputFilePath inputFilePath,
        CourseCsvImportOptions options)
    {
        return ImportCourses(inputFilePath, options, CancellationToken.None);
    }

    public CourseImportResult ImportCourses(
        CsvInputFilePath inputFilePath,
        CourseCsvImportOptions options,
        CancellationToken cancellationToken)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        cancellationToken.ThrowIfCancellationRequested();
        CourseCsvImportState state = new CourseCsvImportState(options);
        if (inputFilePath.IsValid == false)
        {
            CourseImportDiagnostic invalidPathDiagnostic = createFileDiagnostic(
                ECourseImportErrorCode.InvalidInputFilePath,
                CourseImportRawValue.create(getSafePathValue(inputFilePath)),
                "The CSV input file path was not initialized.");
            state.TryAddDiagnostic(invalidPathDiagnostic);
            return createFailedResult(state);
        }

        if (File.Exists(inputFilePath.Value) == false)
        {
            CourseImportDiagnostic fileNotFoundDiagnostic = createFileDiagnostic(
                ECourseImportErrorCode.FileNotFound,
                CourseImportRawValue.create(inputFilePath.Value),
                "The CSV input file does not exist.");
            state.TryAddDiagnostic(fileNotFoundDiagnostic);
            return createFailedResult(state);
        }

        try
        {
            importCoursesFromFile(inputFilePath, state, cancellationToken);
        }
        catch (DecoderFallbackException exception)
        {
            addFileExceptionDiagnostic(
                state,
                ECourseImportErrorCode.InvalidUtf8Encoding,
                inputFilePath,
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            addFileExceptionDiagnostic(
                state,
                ECourseImportErrorCode.FileAccessDenied,
                inputFilePath,
                exception);
        }
        catch (SecurityException exception)
        {
            addFileExceptionDiagnostic(
                state,
                ECourseImportErrorCode.FileAccessDenied,
                inputFilePath,
                exception);
        }
        catch (FileNotFoundException exception)
        {
            addFileExceptionDiagnostic(
                state,
                ECourseImportErrorCode.FileNotFound,
                inputFilePath,
                exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            addFileExceptionDiagnostic(
                state,
                ECourseImportErrorCode.FileNotFound,
                inputFilePath,
                exception);
        }
        catch (IOException exception)
        {
            addFileExceptionDiagnostic(
                state,
                ECourseImportErrorCode.FileReadFailed,
                inputFilePath,
                exception);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (state.Diagnostics.Count > 0)
        {
            return createFailedResult(state);
        }

        if (state.CourseOfferings.Count == 0)
        {
            CourseImportDiagnostic noCoursesDiagnostic = createFileDiagnostic(
                ECourseImportErrorCode.NoCourseOfferings,
                CourseImportRawValue.create(inputFilePath.Value),
                "The CSV file does not contain course offering records.");
            state.TryAddDiagnostic(noCoursesDiagnostic);
            return createFailedResult(state);
        }

        return new CourseImportResult(
            state.CourseOfferings,
            state.Diagnostics,
            state.DiagnosticCollectionCompletion);
    }

    private void importCoursesFromFile(
        CsvInputFilePath inputFilePath,
        CourseCsvImportState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UTF8Encoding strictUtf8Encoding = new UTF8Encoding(false, true);
        using (TextFieldParser parser = new TextFieldParser(
            inputFilePath.Value,
            strictUtf8Encoding,
            true))
        {
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(",");
            parser.HasFieldsEnclosedInQuotes = true;
            parser.TrimWhiteSpace = false;

            cancellationToken.ThrowIfCancellationRequested();
            CourseCsvSchema? schemaOrNull = readSchemaOrNull(
                parser,
                state,
                cancellationToken);
            if (schemaOrNull == null || state.ShouldStopCollectingDiagnostics)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            readCourseOfferings(parser, schemaOrNull, state, cancellationToken);
        }
    }

    private static CourseCsvSchema? readSchemaOrNull(
        TextFieldParser parser,
        CourseCsvImportState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (parser.EndOfData)
        {
            CourseImportDiagnostic missingHeaderDiagnostic = new CourseImportDiagnostic(
                ECourseImportErrorCode.MissingHeader,
                CsvSourcePosition.File,
                ECsvColumn.Header,
                CourseImportRawValue.create(string.Empty),
                "The CSV file is empty.");
            state.TryAddDiagnostic(missingHeaderDiagnostic);
            return null;
        }

        long headerStartLineNumber = parser.LineNumber;
        string[]? headerFieldsOrNull = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            headerFieldsOrNull = parser.ReadFields();
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (MalformedLineException exception)
        {
            CsvSourcePosition sourcePosition = createSourcePosition(
                exception.LineNumber,
                new CsvRowNumber(headerStartLineNumber));
            CourseImportDiagnostic malformedHeaderDiagnostic = new CourseImportDiagnostic(
                ECourseImportErrorCode.MalformedCsvRecord,
                sourcePosition,
                ECsvColumn.Header,
                CourseImportRawValue.create(getSafeErrorLine(parser)),
                exception.Message);
            state.TryAddDiagnostic(malformedHeaderDiagnostic);
            return null;
        }

        if (headerFieldsOrNull == null)
        {
            CourseImportDiagnostic missingHeaderDiagnostic = new CourseImportDiagnostic(
                ECourseImportErrorCode.MissingHeader,
                CsvSourcePosition.File,
                ECsvColumn.Header,
                CourseImportRawValue.create(string.Empty),
                "The CSV file is empty.");
            state.TryAddDiagnostic(missingHeaderDiagnostic);
            return null;
        }

        CourseCsvSchema? schemaOrNull = findSchemaOrNull(headerFieldsOrNull);
        if (schemaOrNull != null)
        {
            return schemaOrNull;
        }

        CsvSourcePosition headerSourcePosition = createSourcePosition(
            headerStartLineNumber,
            new CsvRowNumber(1L));
        CourseImportDiagnostic invalidHeaderDiagnostic = new CourseImportDiagnostic(
            ECourseImportErrorCode.InvalidHeader,
            headerSourcePosition,
            ECsvColumn.Header,
            CourseImportRawValue.create(string.Join(",", headerFieldsOrNull)),
            "Expected CourseId,Section,Name,TimeSlots with an optional final Classroom column.");
        state.TryAddDiagnostic(invalidHeaderDiagnostic);
        return null;
    }

    private void readCourseOfferings(
        TextFieldParser parser,
        CourseCsvSchema schema,
        CourseCsvImportState state,
        CancellationToken cancellationToken)
    {
        while (state.ShouldStopCollectingDiagnostics == false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (parser.EndOfData)
            {
                break;
            }

            long recordStartLineNumber = parser.LineNumber;
            string[]? fieldsOrNull = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                fieldsOrNull = parser.ReadFields();
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (MalformedLineException exception)
            {
                CsvSourcePosition malformedRecordSourcePosition = createSourcePosition(
                    exception.LineNumber,
                    new CsvRowNumber(recordStartLineNumber));
                CourseImportDiagnostic malformedRecordDiagnostic = new CourseImportDiagnostic(
                    ECourseImportErrorCode.MalformedCsvRecord,
                    malformedRecordSourcePosition,
                    ECsvColumn.Record,
                    CourseImportRawValue.create(getSafeErrorLine(parser)),
                    exception.Message);
                state.TryAddDiagnostic(malformedRecordDiagnostic);
                continue;
            }

            if (fieldsOrNull == null)
            {
                break;
            }

            CsvSourcePosition sourcePosition = createSourcePosition(
                recordStartLineNumber,
                new CsvRowNumber(1L));
            if (fieldsOrNull.Length != schema.ColumnCount)
            {
                CourseImportDiagnostic invalidColumnCountDiagnostic = new CourseImportDiagnostic(
                    ECourseImportErrorCode.InvalidColumnCount,
                    sourcePosition,
                    ECsvColumn.Record,
                    CourseImportRawValue.create(string.Join(",", fieldsOrNull)),
                    "The record column count must exactly match the CSV header.");
                state.TryAddDiagnostic(invalidColumnCountDiagnostic);
                continue;
            }

            CourseCsvRecordParseResult recordParseResult = mRecordParser.ParseCourseOffering(
                fieldsOrNull,
                sourcePosition,
                schema,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (recordParseResult.IsSuccessful)
            {
                state.CourseOfferings.Add(recordParseResult.GetCourseOffering());
                continue;
            }

            foreach (CourseImportDiagnostic diagnostic in recordParseResult.Diagnostics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool hasAddedDiagnostic = state.TryAddDiagnostic(diagnostic);
                if (hasAddedDiagnostic == false)
                {
                    break;
                }
            }
        }
    }

    private static CourseCsvSchema? findSchemaOrNull(IReadOnlyList<string> headerFields)
    {
        if (isHeaderMatch(headerFields, FOUR_COLUMN_HEADER))
        {
            return CourseCsvSchema.CreateWithoutClassroomLocation();
        }

        if (isHeaderMatch(headerFields, FIVE_COLUMN_HEADER))
        {
            return CourseCsvSchema.CreateWithClassroomLocation();
        }

        return null;
    }

    private static bool isHeaderMatch(
        IReadOnlyList<string> actualHeader,
        IReadOnlyList<string> expectedHeader)
    {
        if (actualHeader.Count != expectedHeader.Count)
        {
            return false;
        }

        for (int columnIndex = 0; columnIndex < expectedHeader.Count; ++columnIndex)
        {
            if (string.Equals(
                actualHeader[columnIndex],
                expectedHeader[columnIndex],
                StringComparison.Ordinal) == false)
            {
                return false;
            }
        }

        return true;
    }

    private static CsvSourcePosition createSourcePosition(
        long preferredLineNumber,
        CsvRowNumber fallbackRowNumber)
    {
        if (preferredLineNumber <= 0L)
        {
            return CsvSourcePosition.CreateAtRow(fallbackRowNumber);
        }

        CsvRowNumber rowNumber = new CsvRowNumber(preferredLineNumber);
        return CsvSourcePosition.CreateAtRow(rowNumber);
    }

    private static string getSafeErrorLine(TextFieldParser parser)
    {
        string? errorLineOrNull = parser.ErrorLine;
        if (errorLineOrNull == null)
        {
            return string.Empty;
        }

        return errorLineOrNull;
    }

    private static void addFileExceptionDiagnostic(
        CourseCsvImportState state,
        ECourseImportErrorCode errorCode,
        CsvInputFilePath inputFilePath,
        Exception exception)
    {
        CourseImportDiagnostic diagnostic = createFileDiagnostic(
            errorCode,
            CourseImportRawValue.create(inputFilePath.Value),
            exception.Message);
        state.TryAddDiagnostic(diagnostic);
    }

    private static CourseImportResult createFailedResult(CourseCsvImportState state)
    {
        List<CourseOffering> noCourseOfferings = new List<CourseOffering>();
        return new CourseImportResult(
            noCourseOfferings,
            state.Diagnostics,
            state.DiagnosticCollectionCompletion);
    }

    private static CourseImportDiagnostic createFileDiagnostic(
        ECourseImportErrorCode errorCode,
        CourseImportRawValue rawValue,
        string technicalDetails)
    {
        return new CourseImportDiagnostic(
            errorCode,
            CsvSourcePosition.File,
            ECsvColumn.File,
            rawValue,
            technicalDetails);
    }

    private static string getSafePathValue(CsvInputFilePath inputFilePath)
    {
        if (inputFilePath.Value == null)
        {
            return string.Empty;
        }

        return inputFilePath.Value;
    }
}
