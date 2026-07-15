using System;
using System.Globalization;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct EnrollmentCount
{
    public int Value { get; }

    public EnrollmentCount(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Enrollment count cannot be negative.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
