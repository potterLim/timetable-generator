using System;
using System.Globalization;

namespace TimetableGenerator.CatalogJson;

public readonly record struct OfferingSeatCapacity
{
    public int Value { get; }

    public OfferingSeatCapacity(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Seat capacities cannot be negative.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
