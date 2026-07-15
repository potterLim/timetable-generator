using System;
using System.Globalization;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct AcademicPeriod
{
    private const int FIRST_PERIOD = 1;
    private const int LAST_PERIOD = 10;

    public int Value { get; }

    public AcademicPeriod(int value)
    {
        if (value < FIRST_PERIOD || value > LAST_PERIOD)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Academic periods must be between 1 and 10.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
