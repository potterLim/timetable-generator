using System;
using System.Collections.Generic;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarExportPlan
{
    private readonly IReadOnlyList<GoogleCalendarExportEvent> mEvents;

    public PlanId PlanId { get; }

    public PlanName CalendarName { get; }

    public CalendarTimeZoneId TimeZoneId { get; }

    public CalendarUtcOffset UtcOffset { get; }

    public IReadOnlyList<GoogleCalendarExportEvent> Events
    {
        get
        {
            return mEvents;
        }
    }

    public GoogleCalendarExportPlan(
        PlanId planId,
        PlanName calendarName,
        CalendarTimeZoneId timeZoneId,
        CalendarUtcOffset utcOffset,
        IReadOnlyList<GoogleCalendarExportEvent> events)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar exports require a valid plan ID.",
                nameof(planId));
        }

        if (calendarName == null)
        {
            throw new ArgumentNullException(nameof(calendarName));
        }

        if (timeZoneId.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar exports require a valid time-zone ID.",
                nameof(timeZoneId));
        }

        if (utcOffset.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar exports require a valid UTC offset.",
                nameof(utcOffset));
        }

        if (events == null)
        {
            throw new ArgumentNullException(nameof(events));
        }

        HashSet<GoogleCalendarSourceEventId> sourceIds = new HashSet<GoogleCalendarSourceEventId>();
        List<GoogleCalendarExportEvent> eventSnapshot = new List<GoogleCalendarExportEvent>(events.Count);
        foreach (GoogleCalendarExportEvent exportEvent in events)
        {
            if (exportEvent == null)
            {
                throw new ArgumentException(
                    "Google Calendar exports cannot contain null events.",
                    nameof(events));
            }

            if (sourceIds.Add(exportEvent.SourceId) == false)
            {
                throw new ArgumentException(
                    "Google Calendar event source IDs must be unique within a plan.",
                    nameof(events));
            }

            eventSnapshot.Add(exportEvent);
        }

        PlanId = planId;
        CalendarName = calendarName;
        TimeZoneId = timeZoneId;
        UtcOffset = utcOffset;
        mEvents = eventSnapshot.AsReadOnly();
    }

    public static GoogleCalendarExportPlan CreateFromDocument(CalendarExportDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        List<GoogleCalendarExportEvent> events =
            new List<GoogleCalendarExportEvent>(document.Events.Count);
        foreach (RecurringCalendarEvent calendarEvent in document.Events)
        {
            DateOnly firstOccurrenceDate = findFirstOccurrenceDate(
                document.AcademicCalendar,
                calendarEvent.Days);
            events.Add(
                new GoogleCalendarExportEvent(
                    new GoogleCalendarSourceEventId(calendarEvent.Uid.Value),
                    calendarEvent.Content,
                    new GoogleCalendarRecurrenceDateRange(
                        firstOccurrenceDate,
                        document.AcademicCalendar.DateRange.EndDate),
                    calendarEvent.TimeRange,
                    calendarEvent.Days));
        }

        return new GoogleCalendarExportPlan(
            document.PlanId,
            document.CalendarName,
            document.AcademicCalendar.TimeZoneId,
            document.AcademicCalendar.UtcOffset,
            events);
    }

    private static DateOnly findFirstOccurrenceDate(
        AcademicTermCalendarMetadata academicCalendar,
        IReadOnlyList<EDay> days)
    {
        DateOnly firstOccurrenceDate = academicCalendar.FindFirstOccurrenceDate(days[0]);
        for (int dayIndex = 1; dayIndex < days.Count; ++dayIndex)
        {
            DateOnly candidateDate = academicCalendar.FindFirstOccurrenceDate(days[dayIndex]);
            if (candidateDate < firstOccurrenceDate)
            {
                firstOccurrenceDate = candidateDate;
            }
        }

        return firstOccurrenceDate;
    }
}
