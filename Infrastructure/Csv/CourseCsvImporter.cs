using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Infrastructure.Csv;

public sealed class CourseCsvImporter
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
        return ImportCourses(inputFilePath, options);
    }

    public CourseImportResult ImportCourses(
        CsvInputFilePath inputFilePath,
        CourseCsvImportOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        CourseCsvImportState state = new CourseCsvImportState(options);
        if (inputFilePath.IsValid == false)
        {
            CourseImportDiagnostic invalidPathDiagnostic = createFileDiagnostic(
                ECourseImportErrorCode.InvalidInputFilePath,
                getSafePathValue(inputFilePath),
                "The CSV input file path was not initialized.");
            state.TryAddDiagnostic(invalidPathDiagnostic);
            return createFailedResult(state);
        }

        if (File.Exists(inputFilePath.Value) == false)
        {
            CourseImportDiagnostic fileNotFoundDiagnostic = createFileDiagnostic(
                ECourseImportErrorCode.FileNotFound,
                inputFilePath.Value,
                "The CSV input file does not exist.");
            state.TryAddDiagnostic(fileNotFoundDiagnostic);
            return createFailedResult(state);
        }

        try
        {
            importCoursesFromFile(inputFilePath, state);
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

        if (state.Diagnostics.Count > 0)
        {
            return createFailedResult(state);
        }

        if (state.CourseOfferings.Count == 0)
        {
            CourseImportDiagnostic noCoursesDiagnostic = createFileDiagnostic(
                ECourseImportErrorCode.NoCourseOfferings,
                inputFilePath.Value,
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
        CourseCsvImportState state)
    {
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

            CourseCsvSchema schemaOrNull = readSchemaOrNull(parser, state);
            if (schemaOrNull == null || state.ShouldStopCollectingDiagnostics)
            {
                return;
            }

            readCourseOfferings(parser, schemaOrNull, state);
        }
    }

    private static CourseCsvSchema readSchemaOrNull(
        TextFieldParser parser,
        CourseCsvImportState state)
    {
        if (parser.EndOfData)
        {
            CourseImportDiagnostic missingHeaderDiagnostic = new CourseImportDiagnostic(
                ECourseImportErrorCode.MissingHeader,
                CsvSourcePosition.File,
                ECsvColumn.Header,
                string.Empty,
                "The CSV file is empty.");
            state.TryAddDiagnostic(missingHeaderDiagnostic);
            return null;
        }

        long headerStartLineNumber = parser.LineNumber;
        string[] headerFields;

        try
        {
            headerFields = parser.ReadFields();
        }
        catch (MalformedLineException exception)
        {
            CsvSourcePosition sourcePosition = createSourcePosition(
                exception.LineNumber,
                headerStartLineNumber);
            CourseImportDiagnostic malformedHeaderDiagnostic = new CourseImportDiagnostic(
                ECourseImportErrorCode.MalformedCsvRecord,
                sourcePosition,
                ECsvColumn.Header,
                getSafeErrorLine(parser),
                exception.Message);
            state.TryAddDiagnostic(malformedHeaderDiagnostic);
            return null;
        }

        if (headerFields == null)
        {
            CourseImportDiagnostic missingHeaderDiagnostic = new CourseImportDiagnostic(
                ECourseImportErrorCode.MissingHeader,
                CsvSourcePosition.File,
                ECsvColumn.Header,
                string.Empty,
                "The CSV file is empty.");
            state.TryAddDiagnostic(missingHeaderDiagnostic);
            return null;
        }

        CourseCsvSchema schemaOrNull = findSchemaOrNull(headerFields);
        if (schemaOrNull != null)
        {
            return schemaOrNull;
        }

        CsvSourcePosition headerSourcePosition = createSourcePosition(
            headerStartLineNumber,
            1L);
        CourseImportDiagnostic invalidHeaderDiagnostic = new CourseImportDiagnostic(
            ECourseImportErrorCode.InvalidHeader,
            headerSourcePosition,
            ECsvColumn.Header,
            string.Join(",", headerFields),
            "Expected CourseId,Section,Name,TimeSlots with an optional final Classroom column.");
        state.TryAddDiagnostic(invalidHeaderDiagnostic);
        return null;
    }

    private void readCourseOfferings(
        TextFieldParser parser,
        CourseCsvSchema schema,
        CourseCsvImportState state)
    {
        while (parser.EndOfData == false && state.ShouldStopCollectingDiagnostics == false)
        {
            long recordStartLineNumber = parser.LineNumber;
            string[] fields;

            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException exception)
            {
                CsvSourcePosition malformedRecordSourcePosition = createSourcePosition(
                    exception.LineNumber,
                    recordStartLineNumber);
                CourseImportDiagnostic malformedRecordDiagnostic = new CourseImportDiagnostic(
                    ECourseImportErrorCode.MalformedCsvRecord,
                    malformedRecordSourcePosition,
                    ECsvColumn.Record,
                    getSafeErrorLine(parser),
                    exception.Message);
                state.TryAddDiagnostic(malformedRecordDiagnostic);
                continue;
            }

            if (fields == null)
            {
                break;
            }

            CsvSourcePosition sourcePosition = createSourcePosition(recordStartLineNumber, 1L);
            if (fields.Length != schema.ColumnCount)
            {
                CourseImportDiagnostic invalidColumnCountDiagnostic = new CourseImportDiagnostic(
                    ECourseImportErrorCode.InvalidColumnCount,
                    sourcePosition,
                    ECsvColumn.Record,
                    string.Join(",", fields),
                    "The record column count must exactly match the CSV header.");
                state.TryAddDiagnostic(invalidColumnCountDiagnostic);
                continue;
            }

            CourseCsvRecordParseResult recordParseResult = mRecordParser.ParseCourseOffering(
                fields,
                sourcePosition,
                schema);
            if (recordParseResult.IsSuccessful)
            {
                state.CourseOfferings.Add(recordParseResult.GetCourseOffering());
                continue;
            }

            foreach (CourseImportDiagnostic diagnostic in recordParseResult.Diagnostics)
            {
                bool hasAddedDiagnostic = state.TryAddDiagnostic(diagnostic);
                if (hasAddedDiagnostic == false)
                {
                    break;
                }
            }
        }
    }

    private static CourseCsvSchema findSchemaOrNull(IReadOnlyList<string> headerFields)
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
        long fallbackLineNumber)
    {
        long sourceLineNumber = preferredLineNumber;
        if (sourceLineNumber <= 0L)
        {
            sourceLineNumber = fallbackLineNumber;
        }

        Debug.Assert(sourceLineNumber > 0L);
        CsvRowNumber rowNumber = new CsvRowNumber(sourceLineNumber);
        return CsvSourcePosition.CreateAtRow(rowNumber);
    }

    private static string getSafeErrorLine(TextFieldParser parser)
    {
        string errorLine = parser.ErrorLine;
        if (errorLine == null)
        {
            return string.Empty;
        }

        return errorLine;
    }

    private static void addFileExceptionDiagnostic(
        CourseCsvImportState state,
        ECourseImportErrorCode errorCode,
        CsvInputFilePath inputFilePath,
        Exception exception)
    {
        CourseImportDiagnostic diagnostic = createFileDiagnostic(
            errorCode,
            inputFilePath.Value,
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
        string rawValue,
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
