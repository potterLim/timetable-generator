using System;
using System.Globalization;

namespace TimetableGenerator.Application.Scheduling;

public readonly record struct ScheduleRecommendationLimit
{
    private readonly bool mIsUnlimited;

    public static ScheduleRecommendationLimit Unlimited { get; } = new ScheduleRecommendationLimit(int.MaxValue, true);

    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0;
        }
    }

    public bool IsUnlimited
    {
        get
        {
            return mIsUnlimited;
        }
    }

    public ScheduleRecommendationLimit(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Schedule recommendation limits must be positive.");
        }

        Value = value;
        mIsUnlimited = false;
    }

    private ScheduleRecommendationLimit(int value, bool isUnlimited)
    {
        Value = value;
        mIsUnlimited = isUnlimited;
    }

    public override string ToString()
    {
        return IsUnlimited
            ? "Unlimited"
            : Value.ToString(CultureInfo.InvariantCulture);
    }
}
