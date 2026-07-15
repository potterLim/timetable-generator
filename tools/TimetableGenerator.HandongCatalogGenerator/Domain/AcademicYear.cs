using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct AcademicYear
{
    private const int MINIMUM_YEAR = 2_000;
    private const int MAXIMUM_YEAR = 9_999;

    public int Value { get; }

    public AcademicYear(int value)
    {
        if (value < MINIMUM_YEAR || value > MAXIMUM_YEAR)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "The academic year is outside the supported range.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
