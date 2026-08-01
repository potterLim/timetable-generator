using System;
using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarPendingOperation
{
    private readonly IReadOnlyList<AppleCalendarPendingEvent> mDesiredEvents;

    public string OperationId { get; }

    public string PlanId { get; }

    public string DocumentPlanId { get; }

    public string? CalendarIdentifierOrNull { get; }

    public string? ExpectedSourceIdentifierOrNull { get; }

    public string CalendarName { get; }

    public string NormalizedCalendarName { get; }

    public long TermStartsAtUnixSeconds { get; }

    public long TermEndsAtUnixSeconds { get; }

    public long PreparedAtUnixSeconds { get; }

    public IReadOnlyList<AppleCalendarPendingEvent> DesiredEvents
    {
        get
        {
            return mDesiredEvents;
        }
    }

    public AppleCalendarPendingOperation(
        string operationId,
        string planId,
        string documentPlanId,
        string? calendarIdentifierOrNull,
        string? expectedSourceIdentifierOrNull,
        string calendarName,
        string normalizedCalendarName,
        long termStartsAtUnixSeconds,
        long termEndsAtUnixSeconds,
        long preparedAtUnixSeconds,
        IReadOnlyList<AppleCalendarPendingEvent> desiredEvents)
    {
        OperationId = AppleCalendarRegistryValue.RequireGuid(operationId, nameof(operationId));
        PlanId = AppleCalendarRegistryValue.RequirePlanId(planId, nameof(planId));
        DocumentPlanId = AppleCalendarRegistryValue.RequirePlanId(documentPlanId, nameof(documentPlanId));
        CalendarIdentifierOrNull = AppleCalendarRegistryValue.NormalizeOptionalText(calendarIdentifierOrNull);
        ExpectedSourceIdentifierOrNull = AppleCalendarRegistryValue.NormalizeOptionalText(expectedSourceIdentifierOrNull);
        CalendarName = AppleCalendarRegistryValue.RequireText(calendarName, nameof(calendarName));
        NormalizedCalendarName = AppleCalendarRegistryValue.RequireText(normalizedCalendarName, nameof(normalizedCalendarName));
        if (termEndsAtUnixSeconds < termStartsAtUnixSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(termEndsAtUnixSeconds));
        }

        TermStartsAtUnixSeconds = termStartsAtUnixSeconds;
        TermEndsAtUnixSeconds = termEndsAtUnixSeconds;
        if (preparedAtUnixSeconds <= 0 || preparedAtUnixSeconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            throw new ArgumentOutOfRangeException(nameof(preparedAtUnixSeconds));
        }

        PreparedAtUnixSeconds = preparedAtUnixSeconds;
        mDesiredEvents = copyEvents(desiredEvents);
    }

    private static IReadOnlyList<AppleCalendarPendingEvent> copyEvents(IReadOnlyList<AppleCalendarPendingEvent> desiredEvents)
    {
        if (desiredEvents == null)
        {
            throw new ArgumentNullException(nameof(desiredEvents));
        }

        List<AppleCalendarPendingEvent> copiedEvents = new List<AppleCalendarPendingEvent>(desiredEvents.Count);
        HashSet<string> sourceEventHashes = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> fingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach (AppleCalendarPendingEvent? eventOrNull in desiredEvents)
        {
            if (eventOrNull == null
                || sourceEventHashes.Add(eventOrNull.SourceEventHash) == false
                || fingerprints.Add(eventOrNull.Fingerprint) == false)
            {
                throw new ArgumentException("Apple Calendar pending operations require unique desired events.", nameof(desiredEvents));
            }

            copiedEvents.Add(eventOrNull);
        }

        if (copiedEvents.Count == 0)
        {
            throw new ArgumentException("Apple Calendar pending operations require at least one desired event.", nameof(desiredEvents));
        }

        return copiedEvents.AsReadOnly();
    }
}
