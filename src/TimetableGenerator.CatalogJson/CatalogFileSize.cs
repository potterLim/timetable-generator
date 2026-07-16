using System;
using System.Globalization;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogFileSize
{
    public long Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0L;
        }
    }

    public CatalogFileSize(long value)
    {
        if (value <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Catalog file sizes must be positive.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
