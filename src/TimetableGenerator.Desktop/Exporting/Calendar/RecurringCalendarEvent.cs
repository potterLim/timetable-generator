using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class RecurringCalendarEvent
{
    private readonly IReadOnlyList<EDay> mDays;

    public CalendarEventUid Uid { get; }

    public CalendarEventContent Content { get; }

    public DailyTimeRange TimeRange { get; }

    public IReadOnlyList<EDay> Days
    {
        get
        {
            return mDays;
        }
    }

    public RecurringCalendarEvent(CalendarEventUid uid, CalendarEventContent content, DailyTimeRange timeRange, IEnumerable<EDay> days)
    {
        if (uid.IsValid == false)
        {
            throw new ArgumentException("Recurring calendar events require a valid UID.", nameof(uid));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (timeRange.IsValid == false)
        {
            throw new ArgumentException("Recurring calendar events require a valid daily time range.", nameof(timeRange));
        }

        if (days == null)
        {
            throw new ArgumentNullException(nameof(days));
        }

        Uid = uid;
        Content = content;
        TimeRange = timeRange;
        mDays = copyAndValidateDays(days);
    }

    private static IReadOnlyList<EDay> copyAndValidateDays(IEnumerable<EDay> days)
    {
        List<EDay> copiedDays = new List<EDay>();
        HashSet<EDay> uniqueDays = new HashSet<EDay>();
        foreach (EDay day in days)
        {
            ensureSupportedDay(day, days);
            if (uniqueDays.Add(day) == false)
            {
                throw new ArgumentException("Recurring calendar events cannot contain duplicate weekdays.", nameof(days));
            }

            copiedDays.Add(day);
        }

        if (copiedDays.Count == 0)
        {
            throw new ArgumentException("Recurring calendar events require at least one weekday.", nameof(days));
        }

        copiedDays.Sort();
        return copiedDays.AsReadOnly();
    }

    private static void ensureSupportedDay(EDay day, IEnumerable<EDay> days)
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
                throw new ArgumentException("Recurring calendar events require weekdays from Monday through Sunday.", nameof(days));
        }
    }
}
