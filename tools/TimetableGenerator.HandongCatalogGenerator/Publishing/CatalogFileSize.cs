using System;
using System.Globalization;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal readonly record struct CatalogFileSize
{
    public long Value { get; }

    public CatalogFileSize(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Catalog file sizes cannot be negative.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
