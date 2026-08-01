using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarRegistration
{
    private readonly IReadOnlyList<AppleCalendarManagedEventRegistration> mEvents;

    public string PlanId { get; }

    public string CalendarIdentifier { get; }

    public string CalendarName { get; }

    public string NormalizedCalendarName { get; }

    public string SourceIdentifier { get; }

    public long TermStartsAtUnixSeconds { get; }

    public long TermEndsAtUnixSeconds { get; }

    public IReadOnlyList<AppleCalendarManagedEventRegistration> Events
    {
        get
        {
            return mEvents;
        }
    }

    public AppleCalendarRegistration(
        string planId,
        string calendarIdentifier,
        string calendarName,
        string normalizedCalendarName,
        string sourceIdentifier,
        long termStartsAtUnixSeconds,
        long termEndsAtUnixSeconds,
        IReadOnlyList<AppleCalendarManagedEventRegistration> events)
    {
        PlanId = AppleCalendarRegistryValue.RequirePlanId(planId, nameof(planId));
        CalendarIdentifier = AppleCalendarRegistryValue.RequireText(calendarIdentifier, nameof(calendarIdentifier));
        CalendarName = AppleCalendarRegistryValue.RequireText(calendarName, nameof(calendarName));
        NormalizedCalendarName = AppleCalendarRegistryValue.RequireText(normalizedCalendarName, nameof(normalizedCalendarName));
        SourceIdentifier = AppleCalendarRegistryValue.RequireText(sourceIdentifier, nameof(sourceIdentifier));
        if (termEndsAtUnixSeconds < termStartsAtUnixSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(termEndsAtUnixSeconds));
        }

        TermStartsAtUnixSeconds = termStartsAtUnixSeconds;
        TermEndsAtUnixSeconds = termEndsAtUnixSeconds;
        mEvents = copyEvents(events);
    }

    public PlanId GetPlanId()
    {
        return new PlanId(Guid.ParseExact(PlanId, "D"));
    }

    private static IReadOnlyList<AppleCalendarManagedEventRegistration> copyEvents(IReadOnlyList<AppleCalendarManagedEventRegistration> events)
    {
        if (events == null)
        {
            throw new ArgumentNullException(nameof(events));
        }

        List<AppleCalendarManagedEventRegistration> copiedEvents = new List<AppleCalendarManagedEventRegistration>(events.Count);
        HashSet<string> sourceEventHashes = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> calendarItemIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (AppleCalendarManagedEventRegistration? eventOrNull in events)
        {
            if (eventOrNull == null
                || sourceEventHashes.Add(eventOrNull.SourceEventHash) == false
                || calendarItemIdentifiers.Add(eventOrNull.CalendarItemIdentifier) == false)
            {
                throw new ArgumentException("Apple Calendar registrations require unique managed events.", nameof(events));
            }

            copiedEvents.Add(eventOrNull);
        }

        return copiedEvents.AsReadOnly();
    }
}
