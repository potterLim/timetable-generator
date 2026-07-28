using System;
using System.Globalization;

namespace TimetableGenerator.CatalogJson;

public readonly record struct SourceRecordNumber
{
    public long Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0L;
        }
    }

    public SourceRecordNumber(long value)
    {
        if (value <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Source record numbers must be positive.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
