using System;
using System.Globalization;

namespace TimetableGenerator.Domain.Scheduling;

public readonly record struct AcademicPeriod
{
    private const int MINIMUM_PERIOD = 1;
    private const int MAXIMUM_PERIOD = 10;

    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value >= MINIMUM_PERIOD && Value <= MAXIMUM_PERIOD;
        }
    }

    public AcademicPeriod(int value)
    {
        if (value < MINIMUM_PERIOD || value > MAXIMUM_PERIOD)
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
