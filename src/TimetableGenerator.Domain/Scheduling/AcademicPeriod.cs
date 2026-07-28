using System;
using System.Globalization;

namespace TimetableGenerator.Domain.Scheduling;

public readonly record struct AcademicPeriod
{
    public const int MINIMUM_VALUE = 1;

    public const int MAXIMUM_VALUE = 10;

    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value >= MINIMUM_VALUE && Value <= MAXIMUM_VALUE;
        }
    }

    public AcademicPeriod(int value)
    {
        if (value < MINIMUM_VALUE || value > MAXIMUM_VALUE)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Academic periods must be between 1 and 10.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
