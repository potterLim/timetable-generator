using System;

namespace TimetableGenerator.Application.Scheduling;

internal readonly record struct ScheduleSearchPriority :
    IComparable<ScheduleSearchPriority>
{
    public RecommendationScore OptimisticScore { get; }

    public int RemainingGroupCount { get; }

    public long Sequence { get; }

    public ScheduleSearchPriority(RecommendationScore optimisticScore, int remainingGroupCount, long sequence)
    {
        if (remainingGroupCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(remainingGroupCount));
        }

        if (sequence < 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }

        OptimisticScore = optimisticScore;
        RemainingGroupCount = remainingGroupCount;
        Sequence = sequence;
    }

    public int CompareTo(ScheduleSearchPriority other)
    {
        int scoreComparison = OptimisticScore.CompareTo(other.OptimisticScore);
        if (scoreComparison != 0)
        {
            return scoreComparison;
        }

        int depthComparison = RemainingGroupCount.CompareTo(other.RemainingGroupCount);
        if (depthComparison != 0)
        {
            return depthComparison;
        }

        return Sequence.CompareTo(other.Sequence);
    }
}
