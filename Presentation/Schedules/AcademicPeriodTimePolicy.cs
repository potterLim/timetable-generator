using System;
using CorePeriod = TimetableGenerator.Core.Domain.Period;

namespace TimetableGenerator.Presentation.Schedules;

public static class AcademicPeriodTimePolicy
{
    private const int MINUTES_PER_HOUR = 60;
    private const int MINUTES_PER_DAY = 24 * MINUTES_PER_HOUR;
    private const int FIRST_PERIOD_START_HOUR = 8;
    private const int FIRST_PERIOD_START_MINUTE = 30;
    private const int CLASS_DURATION_MINUTES = 75;
    private const int BREAK_DURATION_MINUTES = 15;
    private const int PERIOD_BLOCK_MINUTES = CLASS_DURATION_MINUTES + BREAK_DURATION_MINUTES;
    private const int MAXIMUM_PERIOD_VALUE = 10;

    public static CorePeriod MaximumSupportedPeriod
    {
        get
        {
            return new CorePeriod(MAXIMUM_PERIOD_VALUE);
        }
    }

    public static AcademicPeriodTimeRange GetTimeRange(CorePeriod period)
    {
        if (period.IsValid == false)
        {
            throw new ArgumentException("A valid academic period is required.", nameof(period));
        }

        if (period.Value > MAXIMUM_PERIOD_VALUE)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period),
                period.Value,
                "Academic periods cannot extend beyond period 10.");
        }

        int firstPeriodStartMinutes =
            (FIRST_PERIOD_START_HOUR * MINUTES_PER_HOUR) + FIRST_PERIOD_START_MINUTE;
        int completedPeriodCount = period.Value - 1;
        int periodStartMinutes =
            firstPeriodStartMinutes + (completedPeriodCount * PERIOD_BLOCK_MINUTES);
        int periodEndMinutes = periodStartMinutes + CLASS_DURATION_MINUTES;

        if (periodEndMinutes >= MINUTES_PER_DAY)
        {
            throw new ArgumentOutOfRangeException(
                nameof(period),
                period.Value,
                "Academic periods must start and end within the same day.");
        }

        TimeOnly startTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(periodStartMinutes));
        TimeOnly endTime = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(periodEndMinutes));
        return new AcademicPeriodTimeRange(period, startTime, endTime);
    }
}
