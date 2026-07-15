using System;
using System.Globalization;

namespace TimetableGenerator.Infrastructure.Csv;

public readonly record struct CsvRowNumber
{
    public long Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0L;
        }
    }

    public CsvRowNumber(long value)
    {
        if (value <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "CSV row numbers must be greater than zero.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
