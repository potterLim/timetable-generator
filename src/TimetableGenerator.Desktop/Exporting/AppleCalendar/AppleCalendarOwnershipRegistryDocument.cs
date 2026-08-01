using System;
using System.Collections.Generic;
using System.Linq;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarOwnershipRegistryDocument
{
    public const int CURRENT_SCHEMA_VERSION = 1;

    private readonly IReadOnlyList<AppleCalendarRegistration> mCalendars;

    public int SchemaVersion { get; }

    public IReadOnlyList<AppleCalendarRegistration> Calendars
    {
        get
        {
            return mCalendars;
        }
    }

    public AppleCalendarPendingOperation? PendingOperation { get; }

    public AppleCalendarOwnershipRegistryDocument(int schemaVersion, IReadOnlyList<AppleCalendarRegistration> calendars, AppleCalendarPendingOperation? pendingOperation)
    {
        if (schemaVersion != CURRENT_SCHEMA_VERSION)
        {
            throw new AppleCalendarOwnershipRegistryException("The Apple Calendar ownership registry schema version is not supported.");
        }

        SchemaVersion = schemaVersion;
        mCalendars = copyCalendars(calendars);
        PendingOperation = pendingOperation;
    }

    public static AppleCalendarOwnershipRegistryDocument CreateEmpty()
    {
        return new AppleCalendarOwnershipRegistryDocument(CURRENT_SCHEMA_VERSION, Array.Empty<AppleCalendarRegistration>(), null);
    }

    public AppleCalendarOwnershipRegistryDocument WithPendingOperation(AppleCalendarPendingOperation pendingOperation)
    {
        if (pendingOperation == null)
        {
            throw new ArgumentNullException(nameof(pendingOperation));
        }

        return new AppleCalendarOwnershipRegistryDocument(SchemaVersion, mCalendars, pendingOperation);
    }

    public AppleCalendarOwnershipRegistryDocument ClearPendingOperation()
    {
        return new AppleCalendarOwnershipRegistryDocument(SchemaVersion, mCalendars, null);
    }

    public AppleCalendarOwnershipRegistryDocument RemoveMissingCalendar(string calendarIdentifier)
    {
        string normalizedCalendarIdentifier = AppleCalendarRegistryValue.RequireText(calendarIdentifier, nameof(calendarIdentifier));
        if (PendingOperation != null && string.Equals(PendingOperation.CalendarIdentifierOrNull, normalizedCalendarIdentifier, StringComparison.Ordinal))
        {
            throw new AppleCalendarOwnershipRegistryException("A calendar with a pending Apple Calendar operation cannot be removed from the ownership registry.");
        }

        IReadOnlyList<AppleCalendarRegistration> registrations = mCalendars.Where(registration => string.Equals(registration.CalendarIdentifier, normalizedCalendarIdentifier, StringComparison.Ordinal) == false).ToList().AsReadOnly();
        if (registrations.Count == mCalendars.Count)
        {
            throw new AppleCalendarOwnershipRegistryException("The missing Apple Calendar registration does not exist.");
        }

        return new AppleCalendarOwnershipRegistryDocument(SchemaVersion, registrations, PendingOperation);
    }

    public AppleCalendarOwnershipRegistryDocument CompleteOperation(AppleCalendarRegistration registration)
    {
        if (registration == null)
        {
            throw new ArgumentNullException(nameof(registration));
        }

        List<AppleCalendarRegistration> registrations = mCalendars.Where(existing => string.Equals(existing.CalendarIdentifier, registration.CalendarIdentifier, StringComparison.Ordinal) == false).ToList();
        registrations.Add(registration);
        registrations.Sort(compareRegistrations);
        return new AppleCalendarOwnershipRegistryDocument(SchemaVersion, registrations.AsReadOnly(), null);
    }

    public AppleCalendarOwnershipRegistryDocument RebindCalendar(string previousCalendarIdentifier, AppleCalendarRegistration registration)
    {
        if (registration == null)
        {
            throw new ArgumentNullException(nameof(registration));
        }

        string normalizedPreviousCalendarIdentifier = AppleCalendarRegistryValue.RequireText(previousCalendarIdentifier, nameof(previousCalendarIdentifier));
        AppleCalendarRegistration? previousRegistrationOrNull = mCalendars.SingleOrDefault(existing => string.Equals(existing.CalendarIdentifier, normalizedPreviousCalendarIdentifier, StringComparison.Ordinal));
        if (previousRegistrationOrNull == null
            || string.Equals(previousRegistrationOrNull.PlanId, registration.PlanId, StringComparison.Ordinal) == false
            || mCalendars.Any(existing => string.Equals(existing.CalendarIdentifier, registration.CalendarIdentifier, StringComparison.Ordinal) && string.Equals(existing.CalendarIdentifier, normalizedPreviousCalendarIdentifier, StringComparison.Ordinal) == false))
        {
            throw new AppleCalendarOwnershipRegistryException("The Apple Calendar ownership registry cannot rebind the requested calendar.");
        }

        List<AppleCalendarRegistration> registrations = mCalendars.Where(existing => string.Equals(existing.CalendarIdentifier, normalizedPreviousCalendarIdentifier, StringComparison.Ordinal) == false).ToList();
        registrations.Add(registration);
        registrations.Sort(compareRegistrations);
        AppleCalendarPendingOperation? pendingOperationOrNull = rebindPendingOperationOrNull(PendingOperation, normalizedPreviousCalendarIdentifier, registration);
        return new AppleCalendarOwnershipRegistryDocument(SchemaVersion, registrations.AsReadOnly(), pendingOperationOrNull);
    }

    private static IReadOnlyList<AppleCalendarRegistration> copyCalendars(IReadOnlyList<AppleCalendarRegistration> calendars)
    {
        if (calendars == null)
        {
            throw new ArgumentNullException(nameof(calendars));
        }

        List<AppleCalendarRegistration> copiedCalendars = new List<AppleCalendarRegistration>(calendars.Count);
        HashSet<string> calendarIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (AppleCalendarRegistration? calendarOrNull in calendars)
        {
            if (calendarOrNull == null || calendarIdentifiers.Add(calendarOrNull.CalendarIdentifier) == false)
            {
                throw new AppleCalendarOwnershipRegistryException("The Apple Calendar ownership registry contains duplicate or invalid calendars.");
            }

            copiedCalendars.Add(calendarOrNull);
        }

        copiedCalendars.Sort(compareRegistrations);
        return copiedCalendars.AsReadOnly();
    }

    private static int compareRegistrations(AppleCalendarRegistration left, AppleCalendarRegistration right)
    {
        return string.CompareOrdinal(left.CalendarIdentifier, right.CalendarIdentifier);
    }

    private static AppleCalendarPendingOperation? rebindPendingOperationOrNull(
        AppleCalendarPendingOperation? pendingOperationOrNull,
        string previousCalendarIdentifier,
        AppleCalendarRegistration registration)
    {
        if (pendingOperationOrNull == null
            || string.Equals(pendingOperationOrNull.CalendarIdentifierOrNull, previousCalendarIdentifier, StringComparison.Ordinal) == false)
        {
            return pendingOperationOrNull;
        }

        if (string.Equals(pendingOperationOrNull.PlanId, registration.PlanId, StringComparison.Ordinal) == false
            || (pendingOperationOrNull.ExpectedSourceIdentifierOrNull != null && string.Equals(pendingOperationOrNull.ExpectedSourceIdentifierOrNull, registration.SourceIdentifier, StringComparison.Ordinal) == false))
        {
            throw new AppleCalendarOwnershipRegistryException("The pending Apple Calendar operation cannot be rebound safely.");
        }

        return new AppleCalendarPendingOperation(
            pendingOperationOrNull.OperationId,
            pendingOperationOrNull.PlanId,
            pendingOperationOrNull.DocumentPlanId,
            registration.CalendarIdentifier,
            registration.SourceIdentifier,
            pendingOperationOrNull.CalendarName,
            pendingOperationOrNull.NormalizedCalendarName,
            pendingOperationOrNull.TermStartsAtUnixSeconds,
            pendingOperationOrNull.TermEndsAtUnixSeconds,
            pendingOperationOrNull.PreparedAtUnixSeconds,
            pendingOperationOrNull.DesiredEvents);
    }
}
