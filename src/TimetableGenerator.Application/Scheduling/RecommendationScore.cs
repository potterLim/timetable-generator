using System;
using System.Globalization;

namespace TimetableGenerator.Application.Scheduling;

public readonly record struct RecommendationScore : IComparable<RecommendationScore>
{
    public static readonly RecommendationScore ZERO = new RecommendationScore(0);

    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value >= 0;
        }
    }

    public RecommendationScore(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Recommendation scores cannot be negative.");
        }

        Value = value;
    }

    public RecommendationScore Add(RecommendationScore score)
    {
        int combinedValue = checked(Value + score.Value);
        return new RecommendationScore(combinedValue);
    }

    public int CompareTo(RecommendationScore other)
    {
        return Value.CompareTo(other.Value);
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
