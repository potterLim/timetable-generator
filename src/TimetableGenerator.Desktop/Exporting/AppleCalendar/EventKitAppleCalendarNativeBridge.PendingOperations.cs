using System;
using System.Collections.Generic;
using System.Linq;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed partial class EventKitAppleCalendarNativeBridge
{
    private static void ensureUniqueFingerprints(IReadOnlyList<AppleCalendarRecurringEvent> recurringEvents)
    {
        HashSet<string> fingerprints = new HashSet<string>(StringComparer.Ordinal);
        foreach (AppleCalendarRecurringEvent recurringEvent in recurringEvents)
        {
            if (fingerprints.Add(recurringEvent.Fingerprint) == false)
            {
                throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_event_identity_ambiguous");
            }
        }
    }

    private AppleCalendarOwnershipRegistryDocument preparePendingOperation(AppleCalendarOwnershipRegistryDocument registry, AppleCalendarPendingOperation desiredPendingOperation)
    {
        if (registry.PendingOperation == null)
        {
            AppleCalendarOwnershipRegistryDocument registryWithPending = registry.WithPendingOperation(desiredPendingOperation);
            saveRegistry(registryWithPending, "apple_calendar_registry_prepare_failed");
            return registryWithPending;
        }

        if (pendingOperationsMatch(registry.PendingOperation, desiredPendingOperation) == false)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_pending_operation_conflict");
        }

        return registry;
    }

    private static AppleCalendarPendingOperation createPendingOperation(AppleCalendarExportMutation mutation, IReadOnlyList<AppleCalendarRecurringEvent> recurringEvents)
    {
        List<AppleCalendarPendingEvent> desiredEvents = new List<AppleCalendarPendingEvent>(recurringEvents.Count);
        foreach (AppleCalendarRecurringEvent recurringEvent in recurringEvents)
        {
            desiredEvents.Add(new AppleCalendarPendingEvent(recurringEvent.SourceEventHash, recurringEvent.Fingerprint));
        }

        (long termStartsAtUnixSeconds, long termEndsAtUnixSeconds) = EventKitAppleCalendarRequest.GetTermRange(mutation.Document);
        return new AppleCalendarPendingOperation(
            Guid.NewGuid().ToString("D"),
            mutation.CalendarOwnershipPlanId.ToString(),
            mutation.Document.PlanId.ToString(),
            mutation.ExistingCalendarIdOrNull?.Value,
            mutation.ExpectedSourceIdentifierOrNull,
            mutation.DestinationName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(mutation.DestinationName.Value),
            termStartsAtUnixSeconds,
            termEndsAtUnixSeconds,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            desiredEvents.AsReadOnly());
    }

    private static bool pendingOperationsMatch(AppleCalendarPendingOperation existing, AppleCalendarPendingOperation desired)
    {
        if (string.Equals(existing.PlanId, desired.PlanId, StringComparison.Ordinal) == false
            || string.Equals(existing.DocumentPlanId, desired.DocumentPlanId, StringComparison.Ordinal) == false
            || string.Equals(existing.CalendarIdentifierOrNull, desired.CalendarIdentifierOrNull, StringComparison.Ordinal) == false
            || string.Equals(existing.ExpectedSourceIdentifierOrNull, desired.ExpectedSourceIdentifierOrNull, StringComparison.Ordinal) == false
            || string.Equals(existing.CalendarName, desired.CalendarName, StringComparison.Ordinal) == false
            || string.Equals(existing.NormalizedCalendarName, desired.NormalizedCalendarName, StringComparison.Ordinal) == false
            || existing.TermStartsAtUnixSeconds != desired.TermStartsAtUnixSeconds
            || existing.TermEndsAtUnixSeconds != desired.TermEndsAtUnixSeconds
            || existing.DesiredEvents.Count != desired.DesiredEvents.Count)
        {
            return false;
        }

        for (int index = 0; index < existing.DesiredEvents.Count; ++index)
        {
            AppleCalendarPendingEvent existingEvent = existing.DesiredEvents[index];
            AppleCalendarPendingEvent desiredEvent = desired.DesiredEvents[index];
            if (string.Equals(existingEvent.SourceEventHash, desiredEvent.SourceEventHash, StringComparison.Ordinal) == false
                || string.Equals(existingEvent.Fingerprint, desiredEvent.Fingerprint, StringComparison.Ordinal) == false)
            {
                return false;
            }
        }

        return true;
    }

    private static void validatePendingRegistration(AppleCalendarRegistration? registrationOrNull, AppleCalendarPendingOperation pendingOperation)
    {
        if (registrationOrNull != null
            && (string.Equals(registrationOrNull.PlanId, pendingOperation.PlanId, StringComparison.Ordinal) == false
                || string.Equals(registrationOrNull.SourceIdentifier, pendingOperation.ExpectedSourceIdentifierOrNull, StringComparison.Ordinal) == false))
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.CalendarChanged, "apple_calendar_registered_owner_changed");
        }
    }

    private AppleCalendarNativeExportResult completeOperation(
        AppleCalendarOwnershipRegistryDocument registry,
        AppleCalendarPendingOperation pendingOperation,
        AppleCalendarExportMutation mutation,
        ValidatedApplyResponse validatedResponse,
        EventKitAppleCalendarResponse response)
    {
        if (mutation.Kind == EAppleCalendarExportMutationKind.CreateNew
            && registry.Calendars.Any(registration => string.Equals(registration.CalendarIdentifier, validatedResponse.CalendarIdentifier, StringComparison.Ordinal)))
        {
            throw invalidResponse();
        }

        AppleCalendarRegistration registration = new AppleCalendarRegistration(
            mutation.CalendarOwnershipPlanId.ToString(),
            validatedResponse.CalendarIdentifier,
            validatedResponse.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(validatedResponse.CalendarName.Value),
            validatedResponse.SourceIdentifier,
            pendingOperation.TermStartsAtUnixSeconds,
            pendingOperation.TermEndsAtUnixSeconds,
            validatedResponse.Events);
        saveRegistry(registry.CompleteOperation(registration), "apple_calendar_registry_finalize_failed");
        return new AppleCalendarNativeExportResult(
            new AppleCalendarId(validatedResponse.CalendarIdentifier),
            validatedResponse.CalendarName,
            response.CreatedEventCount,
            response.DeletedEventCount);
    }

    private void completeReconciliation(
        AppleCalendarOwnershipRegistryDocument registry,
        AppleCalendarPendingOperation pendingOperation,
        ValidatedApplyResponse validatedResponse)
    {
        if (pendingOperation.CalendarIdentifierOrNull == null
            && registry.Calendars.Any(registration => string.Equals(registration.CalendarIdentifier, validatedResponse.CalendarIdentifier, StringComparison.Ordinal)))
        {
            throw invalidResponse();
        }

        AppleCalendarRegistration registration = new AppleCalendarRegistration(
            pendingOperation.PlanId,
            validatedResponse.CalendarIdentifier,
            validatedResponse.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(validatedResponse.CalendarName.Value),
            validatedResponse.SourceIdentifier,
            pendingOperation.TermStartsAtUnixSeconds,
            pendingOperation.TermEndsAtUnixSeconds,
            validatedResponse.Events);
        AppleCalendarOwnershipRegistryDocument completedRegistry = registry;
        if (pendingOperation.CalendarIdentifierOrNull != null
            && string.Equals(pendingOperation.CalendarIdentifierOrNull, validatedResponse.CalendarIdentifier, StringComparison.Ordinal) == false)
        {
            try
            {
                completedRegistry = completedRegistry.RebindCalendar(pendingOperation.CalendarIdentifierOrNull, registration);
            }
            catch (AppleCalendarOwnershipRegistryException exception)
            {
                throw invalidResponse(exception);
            }
        }
        saveRegistry(completedRegistry.CompleteOperation(registration), "apple_calendar_registry_finalize_failed");
    }

    private static AppleCalendarRegistration? findExistingRegistrationOrNull(AppleCalendarOwnershipRegistryDocument registry, AppleCalendarExportMutation mutation)
    {
        if (mutation.Kind != EAppleCalendarExportMutationKind.ReplaceExisting)
        {
            return null;
        }

        string calendarIdentifier = mutation.ExistingCalendarIdOrNull!.Value;
        return registry.Calendars.SingleOrDefault(registration => string.Equals(registration.CalendarIdentifier, calendarIdentifier, StringComparison.Ordinal));
    }

    private static AppleCalendarRegistration? findExistingRegistrationOrNull(AppleCalendarOwnershipRegistryDocument registry, string? calendarIdentifierOrNull)
    {
        if (calendarIdentifierOrNull == null)
        {
            return null;
        }

        return registry.Calendars.SingleOrDefault(registration => string.Equals(registration.CalendarIdentifier, calendarIdentifierOrNull, StringComparison.Ordinal));
    }

    private static void validateExistingRegistration(AppleCalendarRegistration? registrationOrNull, AppleCalendarExportMutation mutation)
    {
        if (registrationOrNull != null && registrationOrNull.GetPlanId() != mutation.CalendarOwnershipPlanId)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.CalendarChanged, "apple_calendar_registered_owner_changed");
        }
    }
}
