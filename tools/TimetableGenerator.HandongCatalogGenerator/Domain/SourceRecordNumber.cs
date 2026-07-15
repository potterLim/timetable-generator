using System;
using System.Globalization;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct SourceRecordNumber
{
    private const int FIRST_DATA_RECORD_NUMBER = 2;

    public int Value { get; }

    public SourceRecordNumber(int value)
    {
        if (value < FIRST_DATA_RECORD_NUMBER)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Source data record numbers begin at two after the header row.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
