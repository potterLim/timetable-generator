using System;

namespace TimetableGenerator.Core.Application.Scheduling;

public sealed class ScheduleGenerationOptions
{
    private const int DEFAULT_MAXIMUM_SCHEDULE_COUNT = 10_000;

    public ScheduleCountLimit MaximumScheduleCount { get; }

    public ScheduleGenerationOptions(ScheduleCountLimit maximumScheduleCount)
    {
        if (maximumScheduleCount.IsValid == false)
        {
            throw new ArgumentException("A valid maximum schedule count is required.", nameof(maximumScheduleCount));
        }

        MaximumScheduleCount = maximumScheduleCount;
    }

    public static ScheduleGenerationOptions CreateDefault()
    {
        ScheduleCountLimit maximumScheduleCount = new ScheduleCountLimit(DEFAULT_MAXIMUM_SCHEDULE_COUNT);
        return new ScheduleGenerationOptions(maximumScheduleCount);
    }
}
