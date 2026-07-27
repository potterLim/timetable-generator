using System;
using System.Collections.Generic;
using System.Globalization;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarEventOccurrenceProjector
{
    private const int DAYS_PER_WEEK = 7;
    private const string ISO_OFFSET_FORMAT = "yyyy-MM-dd'T'HH:mm:sszzz";

    public static IReadOnlyList<AppleCalendarAutomationEvent> Project(CalendarExportDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        return Project(document, document.PlanId);
    }

    public static IReadOnlyList<AppleCalendarAutomationEvent> Project(CalendarExportDocument document, PlanId calendarOwnershipPlanId)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (calendarOwnershipPlanId.IsValid == false)
        {
            throw new ArgumentException(
                "Apple Calendar event projection requires a valid calendar ownership plan ID.",
                nameof(calendarOwnershipPlanId));
        }

        List<AppleCalendarAutomationEvent> occurrences = new List<AppleCalendarAutomationEvent>();
        foreach (RecurringCalendarEvent recurringEvent in document.Events)
        {
            foreach (EDay day in recurringEvent.Days)
            {
                appendOccurrences(
                    occurrences,
                    calendarOwnershipPlanId,
                    document.AcademicCalendar,
                    recurringEvent,
                    day);
            }
        }

        occurrences.Sort(compareOccurrences);
        return occurrences.AsReadOnly();
    }

    private static void appendOccurrences(
        ICollection<AppleCalendarAutomationEvent> occurrences,
        PlanId planId,
        AcademicTermCalendarMetadata academicCalendar,
        RecurringCalendarEvent recurringEvent,
        EDay day)
    {
        DateOnly occurrenceDate = academicCalendar.FindFirstOccurrenceDate(day);
        while (occurrenceDate <= academicCalendar.DateRange.EndDate)
        {
            DateTimeOffset startsAt = resolve(academicCalendar, occurrenceDate, recurringEvent.TimeRange.Start);
            DateTimeOffset endsAt = resolve(academicCalendar, occurrenceDate, recurringEvent.TimeRange.End);
            occurrences.Add(
                new AppleCalendarAutomationEvent(
                    planId,
                    recurringEvent.Uid.Value
                        + ":"
                        + occurrenceDate.ToString(
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture),
                    recurringEvent.Content.Summary,
                    recurringEvent.Content.Location,
                    recurringEvent.Content.Description,
                    formatOffsetDateTime(startsAt),
                    formatOffsetDateTime(endsAt)));

            occurrenceDate = occurrenceDate.AddDays(DAYS_PER_WEEK);
        }
    }

    private static DateTimeOffset resolve(
        AcademicTermCalendarMetadata academicCalendar,
        DateOnly occurrenceDate,
        ScheduleTime scheduleTime)
    {
        TimeOnly time = new TimeOnly(scheduleTime.Hour, scheduleTime.Minute);
        return academicCalendar.TimeZoneId.ResolveLocalDateTime(occurrenceDate, time);
    }

    private static string formatOffsetDateTime(DateTimeOffset value)
    {
        return value.ToString(ISO_OFFSET_FORMAT, CultureInfo.InvariantCulture);
    }

    private static int compareOccurrences(
        AppleCalendarAutomationEvent left,
        AppleCalendarAutomationEvent right)
    {
        int startComparison = string.CompareOrdinal(left.StartsAt, right.StartsAt);
        if (startComparison != 0)
        {
            return startComparison;
        }

        return string.CompareOrdinal(left.EventId, right.EventId);
    }
}
