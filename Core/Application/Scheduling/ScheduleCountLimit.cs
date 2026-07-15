using System;
using System.Globalization;

namespace TimetableGenerator.Core.Application.Scheduling;

public readonly record struct ScheduleCountLimit
{
    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0;
        }
    }

    public ScheduleCountLimit(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Schedule count limits must be greater than zero.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
