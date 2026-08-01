using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class EventKitAppleCalendarRegistrationRequest
{
    public string PlanId { get; }

    public string CalendarIdentifier { get; }

    public string CalendarName { get; }

    public string NormalizedCalendarName { get; }

    public string SourceIdentifier { get; }

    public long TermStartsAtUnixSeconds { get; }

    public long TermEndsAtUnixSeconds { get; }

    public IReadOnlyList<EventKitAppleCalendarManagedEventRequest> ManagedEvents { get; }

    public EventKitAppleCalendarRegistrationRequest(
        string planId,
        string calendarIdentifier,
        string calendarName,
        string normalizedCalendarName,
        string sourceIdentifier,
        long termStartsAtUnixSeconds,
        long termEndsAtUnixSeconds,
        IReadOnlyList<EventKitAppleCalendarManagedEventRequest> managedEvents)
    {
        PlanId = planId;
        CalendarIdentifier = calendarIdentifier;
        CalendarName = calendarName;
        NormalizedCalendarName = normalizedCalendarName;
        SourceIdentifier = sourceIdentifier;
        TermStartsAtUnixSeconds = termStartsAtUnixSeconds;
        TermEndsAtUnixSeconds = termEndsAtUnixSeconds;
        ManagedEvents = managedEvents;
    }
}
