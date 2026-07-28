using System;
using System.Globalization;

namespace TimetableGenerator.CatalogJson;

public readonly record struct OfferingEnrollmentCount
{
    public int Value { get; }

    public OfferingEnrollmentCount(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Enrollment counts cannot be negative.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
