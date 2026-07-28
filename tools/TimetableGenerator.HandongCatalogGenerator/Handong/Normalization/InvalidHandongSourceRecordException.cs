using System;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class InvalidHandongSourceRecordException : Exception
{
    public SourceRecordNumber SourceRecordNumber { get; }

    public EHandongColumn Column { get; }

    public InvalidHandongSourceRecordException(SourceRecordNumber sourceRecordNumber, EHandongColumn column, string technicalDetails)
        : base(createMessage(sourceRecordNumber, column, technicalDetails))
    {
        if (Enum.IsDefined(typeof(EHandongColumn), column) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (string.IsNullOrWhiteSpace(technicalDetails))
        {
            throw new ArgumentException("Technical details cannot be empty.", nameof(technicalDetails));
        }

        SourceRecordNumber = sourceRecordNumber;
        Column = column;
    }

    private static string createMessage(SourceRecordNumber sourceRecordNumber, EHandongColumn column, string technicalDetails)
    {
        return "Source record " + sourceRecordNumber + ", column " + column + ": " + technicalDetails;
    }
}
