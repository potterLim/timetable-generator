using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarRecurringEventProjector
{
    public static IReadOnlyList<AppleCalendarRecurringEvent> Project(CalendarExportDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        List<AppleCalendarRecurringEvent> events = new List<AppleCalendarRecurringEvent>(document.Events.Count);
        foreach (RecurringCalendarEvent recurringEvent in document.Events)
        {
            DateOnly firstOccurrenceDate = findFirstOccurrenceDate(document.AcademicCalendar, recurringEvent.Days);
            TimeOnly startTime = new TimeOnly(recurringEvent.TimeRange.Start.Hour, recurringEvent.TimeRange.Start.Minute);
            TimeOnly endTime = new TimeOnly(recurringEvent.TimeRange.End.Hour, recurringEvent.TimeRange.End.Minute);
            DateTimeOffset startsAt = document.AcademicCalendar.TimeZoneId.ResolveLocalDateTime(firstOccurrenceDate, startTime);
            DateTimeOffset endsAt = document.AcademicCalendar.TimeZoneId.ResolveLocalDateTime(firstOccurrenceDate, endTime);
            long recurrenceEndsAtUnixSeconds = document.AcademicCalendar.GetLastIncludedInstantUtc().ToUnixTimeSeconds();
            IReadOnlyList<int> weekdays = convertWeekdays(recurringEvent.Days);
            string sourceEventHash = AppleCalendarRecurringEventFingerprint.CreateSourceEventHash(recurringEvent.Uid.Value);
            string fingerprint = AppleCalendarRecurringEventFingerprint.Create(
                recurringEvent.Content.Summary,
                recurringEvent.Content.Location,
                recurringEvent.Content.Description,
                startsAt.ToUnixTimeSeconds(),
                endsAt.ToUnixTimeSeconds(),
                document.AcademicCalendar.TimeZoneId.Value,
                recurrenceEndsAtUnixSeconds,
                weekdays);
            events.Add(
                new AppleCalendarRecurringEvent(
                    sourceEventHash,
                    fingerprint,
                    recurringEvent.Content.Summary,
                    recurringEvent.Content.Location,
                    recurringEvent.Content.Description,
                    startsAt.ToUnixTimeSeconds(),
                    endsAt.ToUnixTimeSeconds(),
                    recurrenceEndsAtUnixSeconds,
                    document.AcademicCalendar.TimeZoneId.Value,
                    weekdays));
        }

        events.Sort(compareEvents);
        return events.AsReadOnly();
    }

    private static DateOnly findFirstOccurrenceDate(AcademicTermCalendarMetadata academicCalendar, IReadOnlyList<EDay> days)
    {
        DateOnly firstOccurrenceDate = academicCalendar.FindFirstOccurrenceDate(days[0]);
        for (int index = 1; index < days.Count; ++index)
        {
            DateOnly candidateDate = academicCalendar.FindFirstOccurrenceDate(days[index]);
            if (candidateDate < firstOccurrenceDate)
            {
                firstOccurrenceDate = candidateDate;
            }
        }

        return firstOccurrenceDate;
    }

    private static IReadOnlyList<int> convertWeekdays(IReadOnlyList<EDay> days)
    {
        List<int> weekdays = new List<int>(days.Count);
        foreach (EDay day in days)
        {
            weekdays.Add(convertWeekday(day));
        }

        weekdays.Sort();
        return weekdays.AsReadOnly();
    }

    private static int convertWeekday(EDay day)
    {
        switch (day)
        {
            case EDay.Sunday:
                return 1;
            case EDay.Monday:
                return 2;
            case EDay.Tuesday:
                return 3;
            case EDay.Wednesday:
                return 4;
            case EDay.Thursday:
                return 5;
            case EDay.Friday:
                return 6;
            case EDay.Saturday:
                return 7;
            case EDay.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(day), day, "Apple Calendar recurrence requires a supported weekday.");
        }
    }

    private static int compareEvents(AppleCalendarRecurringEvent left, AppleCalendarRecurringEvent right)
    {
        int startComparison = left.StartsAtUnixSeconds.CompareTo(right.StartsAtUnixSeconds);
        if (startComparison != 0)
        {
            return startComparison;
        }

        return string.CompareOrdinal(left.SourceEventHash, right.SourceEventHash);
    }
}
