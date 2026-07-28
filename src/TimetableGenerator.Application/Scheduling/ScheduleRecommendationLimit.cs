using System;
using System.Globalization;

namespace TimetableGenerator.Application.Scheduling;

public readonly record struct ScheduleRecommendationLimit
{
    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0;
        }
    }

    public ScheduleRecommendationLimit(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Schedule recommendation limits must be positive.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
