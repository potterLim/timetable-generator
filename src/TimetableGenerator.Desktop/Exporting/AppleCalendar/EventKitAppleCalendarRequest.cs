using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class EventKitAppleCalendarRequest
{
    public const int CURRENT_SCHEMA_VERSION = 1;
    private const long LEGACY_MIGRATION_PADDING_SECONDS = 366L * 24L * 60L * 60L;

    public int SchemaVersion { get; }

    public string Operation { get; }

    public string RequestedName { get; }

    public string MutationKind { get; }

    public string DestinationName { get; }

    public string NormalizedDestinationName { get; }

    public string ExistingCalendarIdentifier { get; }

    public string ExpectedSourceIdentifier { get; }

    public string RegisteredPlanId { get; }

    public string PlanId { get; }

    public long TermStartsAtUnixSeconds { get; }

    public long TermEndsAtUnixSeconds { get; }

    public long MigrationStartsAtUnixSeconds { get; }

    public long MigrationEndsAtUnixSeconds { get; }

    public long PreparedAtUnixSeconds { get; }

    public IReadOnlyList<EventKitAppleCalendarRegistrationRequest> Registrations { get; }

    public IReadOnlyList<AppleCalendarRecurringEvent> RecurringEvents { get; }

    public IReadOnlyList<AppleCalendarPendingEvent> DesiredEvents { get; }

    public IReadOnlyList<EventKitAppleCalendarManagedEventRequest> ManagedEvents { get; }

    private EventKitAppleCalendarRequest(
        string operation,
        string requestedName,
        string mutationKind,
        string destinationName,
        string normalizedDestinationName,
        string existingCalendarIdentifier,
        string expectedSourceIdentifier,
        string registeredPlanId,
        string planId,
        long termStartsAtUnixSeconds,
        long termEndsAtUnixSeconds,
        long migrationStartsAtUnixSeconds,
        long migrationEndsAtUnixSeconds,
        long preparedAtUnixSeconds,
        IReadOnlyList<EventKitAppleCalendarRegistrationRequest> registrations,
        IReadOnlyList<AppleCalendarRecurringEvent> recurringEvents,
        IReadOnlyList<AppleCalendarPendingEvent> desiredEvents,
        IReadOnlyList<EventKitAppleCalendarManagedEventRequest> managedEvents)
    {
        SchemaVersion = CURRENT_SCHEMA_VERSION;
        Operation = operation;
        RequestedName = requestedName;
        MutationKind = mutationKind;
        DestinationName = destinationName;
        NormalizedDestinationName = normalizedDestinationName;
        ExistingCalendarIdentifier = existingCalendarIdentifier;
        ExpectedSourceIdentifier = expectedSourceIdentifier;
        RegisteredPlanId = registeredPlanId;
        PlanId = planId;
        TermStartsAtUnixSeconds = termStartsAtUnixSeconds;
        TermEndsAtUnixSeconds = termEndsAtUnixSeconds;
        MigrationStartsAtUnixSeconds = migrationStartsAtUnixSeconds;
        MigrationEndsAtUnixSeconds = migrationEndsAtUnixSeconds;
        PreparedAtUnixSeconds = preparedAtUnixSeconds;
        Registrations = registrations;
        RecurringEvents = recurringEvents;
        DesiredEvents = desiredEvents;
        ManagedEvents = managedEvents;
    }

    public static EventKitAppleCalendarRequest CreateList(CalendarExportDocument document, AppleCalendarOwnershipRegistryDocument registry)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (registry == null)
        {
            throw new ArgumentNullException(nameof(registry));
        }

        List<EventKitAppleCalendarRegistrationRequest> registrations = new List<EventKitAppleCalendarRegistrationRequest>(registry.Calendars.Count);
        foreach (AppleCalendarRegistration registration in registry.Calendars)
        {
            List<EventKitAppleCalendarManagedEventRequest> managedEvents = new List<EventKitAppleCalendarManagedEventRequest>(registration.Events.Count);
            foreach (AppleCalendarManagedEventRegistration managedEvent in registration.Events)
            {
                string externalIdentifier = managedEvent.ExternalIdentifierOrNull == null ? string.Empty : managedEvent.ExternalIdentifierOrNull;
                managedEvents.Add(new EventKitAppleCalendarManagedEventRequest(managedEvent.SourceEventHash, managedEvent.CalendarItemIdentifier, externalIdentifier, managedEvent.Fingerprint));
            }

            registrations.Add(
                new EventKitAppleCalendarRegistrationRequest(
                    registration.PlanId,
                    registration.CalendarIdentifier,
                    registration.CalendarName,
                    registration.NormalizedCalendarName,
                    registration.SourceIdentifier,
                    registration.TermStartsAtUnixSeconds,
                    registration.TermEndsAtUnixSeconds,
                    managedEvents.AsReadOnly()));
        }

        (long termStartsAtUnixSeconds, long termEndsAtUnixSeconds) = getTermRange(document);
        (long migrationStartsAtUnixSeconds, long migrationEndsAtUnixSeconds) = getLegacyMigrationRange(termStartsAtUnixSeconds, termEndsAtUnixSeconds);
        return new EventKitAppleCalendarRequest(
            "list",
            document.CalendarName.Value,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            document.PlanId.ToString(),
            termStartsAtUnixSeconds,
            termEndsAtUnixSeconds,
            migrationStartsAtUnixSeconds,
            migrationEndsAtUnixSeconds,
            0,
            registrations.AsReadOnly(),
            Array.Empty<AppleCalendarRecurringEvent>(),
            Array.Empty<AppleCalendarPendingEvent>(),
            Array.Empty<EventKitAppleCalendarManagedEventRequest>());
    }

    public static EventKitAppleCalendarRequest CreateApply(
        AppleCalendarExportMutation mutation,
        string expectedSourceIdentifier,
        string registeredPlanId,
        long preparedAtUnixSeconds,
        IReadOnlyList<AppleCalendarRecurringEvent> recurringEvents,
        IReadOnlyList<AppleCalendarManagedEventRegistration> managedEvents)
    {
        if (mutation == null)
        {
            throw new ArgumentNullException(nameof(mutation));
        }

        if (expectedSourceIdentifier == null)
        {
            throw new ArgumentNullException(nameof(expectedSourceIdentifier));
        }

        if (registeredPlanId == null)
        {
            throw new ArgumentNullException(nameof(registeredPlanId));
        }

        if (recurringEvents == null)
        {
            throw new ArgumentNullException(nameof(recurringEvents));
        }

        if (managedEvents == null)
        {
            throw new ArgumentNullException(nameof(managedEvents));
        }

        string mutationKind = getMutationKind(mutation.Kind);
        string existingCalendarIdentifier = mutation.ExistingCalendarIdOrNull == null ? string.Empty : mutation.ExistingCalendarIdOrNull.Value;
        List<EventKitAppleCalendarManagedEventRequest> managedEventRequests = new List<EventKitAppleCalendarManagedEventRequest>(managedEvents.Count);
        foreach (AppleCalendarManagedEventRegistration managedEvent in managedEvents)
        {
            string externalIdentifier = managedEvent.ExternalIdentifierOrNull == null ? string.Empty : managedEvent.ExternalIdentifierOrNull;
            managedEventRequests.Add(new EventKitAppleCalendarManagedEventRequest(managedEvent.SourceEventHash, managedEvent.CalendarItemIdentifier, externalIdentifier, managedEvent.Fingerprint));
        }

        (long termStartsAtUnixSeconds, long termEndsAtUnixSeconds) = getTermRange(mutation.Document);
        (long migrationStartsAtUnixSeconds, long migrationEndsAtUnixSeconds) = getLegacyMigrationRange(termStartsAtUnixSeconds, termEndsAtUnixSeconds);
        return new EventKitAppleCalendarRequest(
            "apply",
            string.Empty,
            mutationKind,
            mutation.DestinationName.Value,
            NormalizeCalendarName(mutation.DestinationName.Value),
            existingCalendarIdentifier,
            expectedSourceIdentifier,
            registeredPlanId,
            mutation.CalendarOwnershipPlanId.ToString(),
            termStartsAtUnixSeconds,
            termEndsAtUnixSeconds,
            migrationStartsAtUnixSeconds,
            migrationEndsAtUnixSeconds,
            preparedAtUnixSeconds,
            Array.Empty<EventKitAppleCalendarRegistrationRequest>(),
            recurringEvents,
            Array.Empty<AppleCalendarPendingEvent>(),
            managedEventRequests.AsReadOnly());
    }

    public static EventKitAppleCalendarRequest CreateReconcile(
        AppleCalendarPendingOperation pendingOperation,
        string expectedSourceIdentifier,
        string registeredPlanId,
        IReadOnlyList<AppleCalendarManagedEventRegistration> managedEvents)
    {
        if (pendingOperation == null)
        {
            throw new ArgumentNullException(nameof(pendingOperation));
        }

        if (expectedSourceIdentifier == null)
        {
            throw new ArgumentNullException(nameof(expectedSourceIdentifier));
        }

        if (registeredPlanId == null)
        {
            throw new ArgumentNullException(nameof(registeredPlanId));
        }

        if (managedEvents == null)
        {
            throw new ArgumentNullException(nameof(managedEvents));
        }

        string mutationKind = pendingOperation.CalendarIdentifierOrNull == null ? "create" : "replace";
        (long migrationStartsAtUnixSeconds, long migrationEndsAtUnixSeconds) = getLegacyMigrationRange(pendingOperation.TermStartsAtUnixSeconds, pendingOperation.TermEndsAtUnixSeconds);
        List<EventKitAppleCalendarManagedEventRequest> managedEventRequests = new List<EventKitAppleCalendarManagedEventRequest>(managedEvents.Count);
        foreach (AppleCalendarManagedEventRegistration managedEvent in managedEvents)
        {
            string externalIdentifier = managedEvent.ExternalIdentifierOrNull == null ? string.Empty : managedEvent.ExternalIdentifierOrNull;
            managedEventRequests.Add(new EventKitAppleCalendarManagedEventRequest(managedEvent.SourceEventHash, managedEvent.CalendarItemIdentifier, externalIdentifier, managedEvent.Fingerprint));
        }

        string calendarIdentifier = pendingOperation.CalendarIdentifierOrNull == null ? string.Empty : pendingOperation.CalendarIdentifierOrNull;
        return new EventKitAppleCalendarRequest(
            "reconcile",
            string.Empty,
            mutationKind,
            pendingOperation.CalendarName,
            pendingOperation.NormalizedCalendarName,
            calendarIdentifier,
            expectedSourceIdentifier,
            registeredPlanId,
            pendingOperation.PlanId,
            pendingOperation.TermStartsAtUnixSeconds,
            pendingOperation.TermEndsAtUnixSeconds,
            migrationStartsAtUnixSeconds,
            migrationEndsAtUnixSeconds,
            pendingOperation.PreparedAtUnixSeconds,
            Array.Empty<EventKitAppleCalendarRegistrationRequest>(),
            Array.Empty<AppleCalendarRecurringEvent>(),
            pendingOperation.DesiredEvents,
            managedEventRequests.AsReadOnly());
    }

    public static string NormalizeCalendarName(string value)
    {
        return CalendarNameConflictPolicy.normalizeName(value);
    }

    public static (long StartsAtUnixSeconds, long EndsAtUnixSeconds) GetTermRange(CalendarExportDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        return getTermRange(document);
    }

    internal static (long StartsAtUnixSeconds, long EndsAtUnixSeconds) getLegacyMigrationRange(long termStartsAtUnixSeconds, long termEndsAtUnixSeconds)
    {
        if (termEndsAtUnixSeconds < termStartsAtUnixSeconds || termEndsAtUnixSeconds == long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(termEndsAtUnixSeconds));
        }

        long migrationStartsAtUnixSeconds = termStartsAtUnixSeconds < long.MinValue + LEGACY_MIGRATION_PADDING_SECONDS
            ? long.MinValue
            : termStartsAtUnixSeconds - LEGACY_MIGRATION_PADDING_SECONDS;
        long migrationEndsAtUnixSeconds = termEndsAtUnixSeconds > long.MaxValue - 1L - LEGACY_MIGRATION_PADDING_SECONDS
            ? long.MaxValue - 1L
            : termEndsAtUnixSeconds + LEGACY_MIGRATION_PADDING_SECONDS;
        return (migrationStartsAtUnixSeconds, migrationEndsAtUnixSeconds);
    }

    private static string getMutationKind(EAppleCalendarExportMutationKind kind)
    {
        switch (kind)
        {
            case EAppleCalendarExportMutationKind.CreateNew:
                return "create";
            case EAppleCalendarExportMutationKind.ReplaceExisting:
                return "replace";
            case EAppleCalendarExportMutationKind.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "EventKit requires a supported calendar mutation.");
        }
    }

    private static (long StartsAtUnixSeconds, long EndsAtUnixSeconds) getTermRange(CalendarExportDocument document)
    {
        DateTimeOffset termStart = document.AcademicCalendar.TimeZoneId.ResolveLocalDateTime(document.AcademicCalendar.DateRange.StartDate, TimeOnly.MinValue);
        return (termStart.ToUnixTimeSeconds(), document.AcademicCalendar.GetLastIncludedInstantUtc().ToUnixTimeSeconds());
    }
}
