using System;

namespace TimetableGenerator.Infrastructure.Csv;

public sealed class CourseImportDiagnostic
{
    public ECourseImportErrorCode ErrorCode { get; }

    public CsvSourcePosition SourcePosition { get; }

    public ECsvColumn Column { get; }

    public string RawValue { get; }

    public string TechnicalDetails { get; }

    internal CourseImportDiagnostic(
        ECourseImportErrorCode errorCode,
        CsvSourcePosition sourcePosition,
        ECsvColumn column,
        string rawValue,
        string technicalDetails)
    {
        if (Enum.IsDefined(typeof(ECourseImportErrorCode), errorCode) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(errorCode));
        }

        if (Enum.IsDefined(typeof(ECsvColumn), column) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (rawValue == null)
        {
            throw new ArgumentNullException(nameof(rawValue));
        }

        if (technicalDetails == null)
        {
            throw new ArgumentNullException(nameof(technicalDetails));
        }

        ErrorCode = errorCode;
        SourcePosition = sourcePosition;
        Column = column;
        RawValue = rawValue;
        TechnicalDetails = technicalDetails;
    }
}
