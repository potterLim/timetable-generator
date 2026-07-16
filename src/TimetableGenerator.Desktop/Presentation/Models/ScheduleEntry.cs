using System;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal abstract class ScheduleEntry
{
    public EDay Day { get; }

    public DailyTimeRange TimeRange { get; }

    protected ScheduleEntry(EDay day, DailyTimeRange timeRange)
    {
        ensureSupportedDay(day);
        if (timeRange.IsValid == false)
        {
            throw new ArgumentException(
                "Schedule entries require a valid daily time range.",
                nameof(timeRange));
        }

        Day = day;
        TimeRange = timeRange;
    }

    private static void ensureSupportedDay(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
            case EDay.Tuesday:
            case EDay.Wednesday:
            case EDay.Thursday:
            case EDay.Friday:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "The planning workspace supports weekdays only.");
        }
    }
}
