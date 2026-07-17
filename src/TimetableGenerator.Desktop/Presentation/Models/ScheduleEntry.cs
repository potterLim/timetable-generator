using System;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal abstract class ScheduleEntry
{
    public EDay Day { get; }

    public DailyTimeRange TimeRange { get; }

    protected ScheduleEntry(EDay day, DailyTimeRange timeRange)
    {
        ensureDefinedDay(day);
        if (timeRange.IsValid == false)
        {
            throw new ArgumentException(
                "Schedule entries require a valid daily time range.",
                nameof(timeRange));
        }

        Day = day;
        TimeRange = timeRange;
    }

    private static void ensureDefinedDay(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
            case EDay.Tuesday:
            case EDay.Wednesday:
            case EDay.Thursday:
            case EDay.Friday:
            case EDay.Saturday:
            case EDay.Sunday:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Schedule entries require a defined day of the week.");
        }
    }
}
