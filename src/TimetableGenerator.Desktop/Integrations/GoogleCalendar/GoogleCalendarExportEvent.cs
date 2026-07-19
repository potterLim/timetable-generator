using System;
using System.Collections.Generic;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarExportEvent
{
    private readonly IReadOnlyList<EDay> mDays;

    public GoogleCalendarSourceEventId SourceId { get; }

    public CalendarEventContent Content { get; }

    public GoogleCalendarRecurrenceDateRange RecurrenceDateRange { get; }

    public DailyTimeRange TimeRange { get; }

    public string Title
    {
        get
        {
            return Content.Summary;
        }
    }

    public string Description
    {
        get
        {
            return Content.Description;
        }
    }

    public string Location
    {
        get
        {
            return Content.Location;
        }
    }

    public DateOnly FirstOccurrenceDate
    {
        get
        {
            return RecurrenceDateRange.FirstOccurrenceDate;
        }
    }

    public TimeOnly StartTime
    {
        get
        {
            return new TimeOnly(TimeRange.Start.Hour, TimeRange.Start.Minute);
        }
    }

    public TimeOnly EndTime
    {
        get
        {
            return new TimeOnly(TimeRange.End.Hour, TimeRange.End.Minute);
        }
    }

    public DateOnly LastOccurrenceDate
    {
        get
        {
            return RecurrenceDateRange.LastOccurrenceDate;
        }
    }

    public IReadOnlyList<EDay> Days
    {
        get
        {
            return mDays;
        }
    }

    public GoogleCalendarExportEvent(
        GoogleCalendarSourceEventId sourceId,
        CalendarEventContent content,
        GoogleCalendarRecurrenceDateRange recurrenceDateRange,
        DailyTimeRange timeRange,
        IEnumerable<EDay> days)
    {
        if (sourceId == null)
        {
            throw new ArgumentNullException(nameof(sourceId));
        }

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (recurrenceDateRange == null)
        {
            throw new ArgumentNullException(nameof(recurrenceDateRange));
        }

        if (timeRange.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar events require a valid daily time range.",
                nameof(timeRange));
        }

        if (days == null)
        {
            throw new ArgumentNullException(nameof(days));
        }

        List<EDay> daySnapshot = new List<EDay>();
        HashSet<EDay> uniqueDays = new HashSet<EDay>();
        foreach (EDay day in days)
        {
            ensureSupportedDay(day);
            if (uniqueDays.Add(day) == false)
            {
                throw new ArgumentException(
                    "Google Calendar events cannot repeat a weekday.",
                    nameof(days));
            }

            daySnapshot.Add(day);
        }

        if (daySnapshot.Count == 0)
        {
            throw new ArgumentException(
                "Google Calendar events require at least one weekday.",
                nameof(days));
        }

        daySnapshot.Sort();
        EDay firstOccurrenceDay = convertToDay(
            recurrenceDateRange.FirstOccurrenceDate.DayOfWeek);
        if (uniqueDays.Contains(firstOccurrenceDay) == false)
        {
            throw new ArgumentException(
                "The first occurrence date must match one of the event weekdays.",
                nameof(recurrenceDateRange));
        }

        SourceId = sourceId;
        Content = content;
        RecurrenceDateRange = recurrenceDateRange;
        TimeRange = timeRange;
        mDays = daySnapshot.AsReadOnly();
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
            case EDay.Saturday:
            case EDay.Sunday:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Google Calendar events require a weekday from Monday through Sunday.");
        }
    }

    private static EDay convertToDay(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => EDay.Monday,
            DayOfWeek.Tuesday => EDay.Tuesday,
            DayOfWeek.Wednesday => EDay.Wednesday,
            DayOfWeek.Thursday => EDay.Thursday,
            DayOfWeek.Friday => EDay.Friday,
            DayOfWeek.Saturday => EDay.Saturday,
            DayOfWeek.Sunday => EDay.Sunday,
            _ => throw new ArgumentOutOfRangeException(nameof(day)),
        };
    }
}
