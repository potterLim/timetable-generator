using System;
using System.Collections.Generic;
using System.Linq;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed partial class EventKitAppleCalendarNativeBridge
{
    private static ValidatedApplyResponse validateReconciliationResponse(
        EventKitAppleCalendarResponse response,
        AppleCalendarPendingOperation pendingOperation,
        AppleCalendarRegistration? existingRegistrationOrNull)
    {
        if (string.IsNullOrWhiteSpace(response.CalendarIdentifier)
            || string.IsNullOrWhiteSpace(response.CalendarName)
            || string.IsNullOrWhiteSpace(response.SourceIdentifier)
            || response.Events == null
            || response.CreatedEventCount != pendingOperation.DesiredEvents.Count
            || response.DeletedEventCount != 0
            || (pendingOperation.CalendarIdentifierOrNull != null && string.Equals(response.CalendarIdentifier, pendingOperation.CalendarIdentifierOrNull, StringComparison.Ordinal) == false && existingRegistrationOrNull == null)
            || (pendingOperation.ExpectedSourceIdentifierOrNull != null && string.Equals(response.SourceIdentifier, pendingOperation.ExpectedSourceIdentifierOrNull, StringComparison.Ordinal) == false)
            || (existingRegistrationOrNull != null && string.Equals(response.SourceIdentifier, existingRegistrationOrNull.SourceIdentifier, StringComparison.Ordinal) == false))
        {
            throw invalidResponse();
        }

        PlanName responseCalendarName;
        try
        {
            responseCalendarName = new PlanName(response.CalendarName);
        }
        catch (ArgumentException exception)
        {
            throw invalidResponse(exception);
        }

        if (string.Equals(EventKitAppleCalendarRequest.NormalizeCalendarName(responseCalendarName.Value), pendingOperation.NormalizedCalendarName, StringComparison.Ordinal) == false)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.CalendarChanged, "apple_calendar_destination_changed");
        }

        Dictionary<string, AppleCalendarPendingEvent> desiredEvents = pendingOperation.DesiredEvents.ToDictionary(pendingEvent => pendingEvent.SourceEventHash, StringComparer.Ordinal);
        List<AppleCalendarManagedEventRegistration> registeredEvents = new List<AppleCalendarManagedEventRegistration>(response.Events.Count);
        HashSet<string> responseSourceHashes = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> calendarItemIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (EventKitAppleCalendarEventResponse? eventOrNull in response.Events)
            {
                if (eventOrNull == null
                    || string.IsNullOrWhiteSpace(eventOrNull.SourceEventHash)
                    || string.IsNullOrWhiteSpace(eventOrNull.CalendarItemIdentifier)
                    || string.IsNullOrWhiteSpace(eventOrNull.Fingerprint)
                    || responseSourceHashes.Add(eventOrNull.SourceEventHash) == false
                    || calendarItemIdentifiers.Add(eventOrNull.CalendarItemIdentifier) == false)
                {
                    throw invalidResponse();
                }

                AppleCalendarPendingEvent? desiredEventOrNull;
                if (desiredEvents.TryGetValue(eventOrNull.SourceEventHash, out desiredEventOrNull) == false
                    || string.Equals(eventOrNull.Fingerprint, desiredEventOrNull.Fingerprint, StringComparison.Ordinal) == false)
                {
                    throw invalidResponse();
                }

                registeredEvents.Add(new AppleCalendarManagedEventRegistration(eventOrNull.SourceEventHash, eventOrNull.CalendarItemIdentifier, eventOrNull.ExternalIdentifier, eventOrNull.Fingerprint));
            }
        }
        catch (AppleCalendarNativeBridgeException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw invalidResponse(exception);
        }

        if (responseSourceHashes.Count != desiredEvents.Count)
        {
            throw invalidResponse();
        }

        return new ValidatedApplyResponse(response.CalendarIdentifier, responseCalendarName, response.SourceIdentifier, registeredEvents.AsReadOnly());
    }

    private static ValidatedApplyResponse validateApplyResponse(
        EventKitAppleCalendarResponse response,
        AppleCalendarExportMutation mutation,
        IReadOnlyList<AppleCalendarRecurringEvent> recurringEvents,
        AppleCalendarRegistration? existingRegistrationOrNull)
    {
        if (string.IsNullOrWhiteSpace(response.CalendarIdentifier)
            || string.IsNullOrWhiteSpace(response.CalendarName)
            || string.IsNullOrWhiteSpace(response.SourceIdentifier)
            || response.Events == null
            || response.CreatedEventCount != recurringEvents.Count
            || response.DeletedEventCount < 0
            || (mutation.Kind == EAppleCalendarExportMutationKind.CreateNew && response.DeletedEventCount != 0)
            || (mutation.Kind == EAppleCalendarExportMutationKind.ReplaceExisting && string.Equals(response.CalendarIdentifier, mutation.ExistingCalendarIdOrNull!.Value, StringComparison.Ordinal) == false)
            || (mutation.ExpectedSourceIdentifierOrNull != null && string.Equals(response.SourceIdentifier, mutation.ExpectedSourceIdentifierOrNull, StringComparison.Ordinal) == false)
            || (existingRegistrationOrNull != null && string.Equals(response.SourceIdentifier, existingRegistrationOrNull.SourceIdentifier, StringComparison.Ordinal) == false))
        {
            throw invalidResponse();
        }

        PlanName responseCalendarName;
        try
        {
            responseCalendarName = new PlanName(response.CalendarName);
        }
        catch (ArgumentException exception)
        {
            throw invalidResponse(exception);
        }

        if (string.Equals(EventKitAppleCalendarRequest.NormalizeCalendarName(responseCalendarName.Value), EventKitAppleCalendarRequest.NormalizeCalendarName(mutation.DestinationName.Value), StringComparison.Ordinal) == false)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.CalendarChanged, "apple_calendar_destination_changed");
        }

        Dictionary<string, AppleCalendarRecurringEvent> desiredEvents = recurringEvents.ToDictionary(recurringEvent => recurringEvent.SourceEventHash, StringComparer.Ordinal);
        List<AppleCalendarManagedEventRegistration> registeredEvents = new List<AppleCalendarManagedEventRegistration>(response.Events.Count);
        HashSet<string> responseSourceHashes = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> calendarItemIdentifiers = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (EventKitAppleCalendarEventResponse? eventOrNull in response.Events)
            {
                if (eventOrNull == null
                    || string.IsNullOrWhiteSpace(eventOrNull.SourceEventHash)
                    || string.IsNullOrWhiteSpace(eventOrNull.CalendarItemIdentifier)
                    || string.IsNullOrWhiteSpace(eventOrNull.Fingerprint)
                    || responseSourceHashes.Add(eventOrNull.SourceEventHash) == false
                    || calendarItemIdentifiers.Add(eventOrNull.CalendarItemIdentifier) == false)
                {
                    throw invalidResponse();
                }

                AppleCalendarRecurringEvent? desiredEventOrNull;
                if (desiredEvents.TryGetValue(eventOrNull.SourceEventHash, out desiredEventOrNull) == false
                    || string.Equals(eventOrNull.Fingerprint, desiredEventOrNull.Fingerprint, StringComparison.Ordinal) == false)
                {
                    throw invalidResponse();
                }

                registeredEvents.Add(new AppleCalendarManagedEventRegistration(eventOrNull.SourceEventHash, eventOrNull.CalendarItemIdentifier, eventOrNull.ExternalIdentifier, eventOrNull.Fingerprint));
            }
        }
        catch (AppleCalendarNativeBridgeException)
        {
            throw;
        }
        catch (ArgumentException exception)
        {
            throw invalidResponse(exception);
        }

        if (responseSourceHashes.Count != desiredEvents.Count)
        {
            throw invalidResponse();
        }

        return new ValidatedApplyResponse(response.CalendarIdentifier, responseCalendarName, response.SourceIdentifier, registeredEvents.AsReadOnly());
    }

    private static void ensureSuccessfulResponse(EventKitAppleCalendarResponse response)
    {
        if (response.SchemaVersion != EventKitAppleCalendarRequest.CURRENT_SCHEMA_VERSION)
        {
            throw invalidResponse();
        }

        switch (response.Status)
        {
            case "ok":
                return;
            case "access_denied":
                throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.AccessDenied, getDiagnosticCode(response, "apple_calendar_access_denied"));
            case "calendar_changed":
                throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.CalendarChanged, getDiagnosticCode(response, "apple_calendar_destination_changed"));
            case "operation_failed":
            case "invalid_request":
                throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, getDiagnosticCode(response, "apple_calendar_native_operation_failed"));
            default:
                throw invalidResponse();
        }
    }

    private static EReconciliationNotFoundKind getReconciliationNotFoundKind(EventKitAppleCalendarResponse response)
    {
        if (response.SchemaVersion != EventKitAppleCalendarRequest.CURRENT_SCHEMA_VERSION)
        {
            throw invalidResponse();
        }

        if (string.Equals(response.Status, "not_found", StringComparison.Ordinal) == false)
        {
            return EReconciliationNotFoundKind.None;
        }

        switch (response.DiagnosticCode)
        {
            case "eventkit_reconciliation_not_found":
                return EReconciliationNotFoundKind.OperationNotCommitted;
            case "eventkit_reconciliation_calendar_not_found":
                return EReconciliationNotFoundKind.CalendarMissing;
            default:
                throw invalidResponse();
        }
    }

    private static bool isDefinitelyUncommittedApplyResponse(EventKitAppleCalendarResponse response)
    {
        if (response.SchemaVersion != EventKitAppleCalendarRequest.CURRENT_SCHEMA_VERSION)
        {
            return false;
        }

        switch (response.Status)
        {
            case "access_denied":
                return string.Equals(response.DiagnosticCode, "eventkit_calendar_access_denied", StringComparison.Ordinal);
            case "calendar_changed":
                return string.Equals(response.DiagnosticCode, "eventkit_calendar_destination_changed", StringComparison.Ordinal)
                    || string.Equals(response.DiagnosticCode, "eventkit_calendar_ownership_changed", StringComparison.Ordinal)
                    || string.Equals(response.DiagnosticCode, "eventkit_calendar_managed_events_changed", StringComparison.Ordinal);
            case "invalid_request":
                switch (response.DiagnosticCode)
                {
                    case "eventkit_request_array_invalid":
                    case "eventkit_request_create_precondition_invalid":
                    case "eventkit_request_destination_name_invalid":
                    case "eventkit_request_event_fingerprint_duplicate":
                    case "eventkit_request_event_invalid":
                    case "eventkit_request_events_empty":
                    case "eventkit_request_hash_invalid":
                    case "eventkit_request_integer_invalid":
                    case "eventkit_request_json_invalid":
                    case "eventkit_request_managed_event_duplicate":
                    case "eventkit_request_managed_event_invalid":
                    case "eventkit_request_migration_range_invalid":
                    case "eventkit_request_mutation_kind_invalid":
                    case "eventkit_request_operation_unsupported":
                    case "eventkit_request_plan_id_invalid":
                    case "eventkit_request_registered_plan_id_invalid":
                    case "eventkit_request_replacement_precondition_invalid":
                    case "eventkit_request_schema_version_unsupported":
                    case "eventkit_request_size_invalid":
                    case "eventkit_request_string_invalid":
                    case "eventkit_request_term_range_invalid":
                    case "eventkit_request_weekday_invalid":
                        return true;
                    default:
                        return false;
                }
            case "operation_failed":
                switch (response.DiagnosticCode)
                {
                    case "eventkit_calendar_access_request_timed_out":
                    case "eventkit_calendar_access_request_failed":
                    case "eventkit_calendar_source_unavailable":
                    case "eventkit_calendar_create_failed":
                    case "eventkit_calendar_event_delete_failed":
                    case "eventkit_calendar_event_create_failed":
                        return true;
                    default:
                        return false;
                }
            default:
                return false;
        }
    }

    private static string getDiagnosticCode(EventKitAppleCalendarResponse response, string defaultCode)
    {
        if (string.IsNullOrWhiteSpace(response.DiagnosticCode))
        {
            return defaultCode;
        }

        return response.DiagnosticCode.Trim();
    }

    private static PlanId? parseOptionalPlanId(string? valueOrNull)
    {
        if (string.IsNullOrWhiteSpace(valueOrNull))
        {
            return null;
        }

        Guid value;
        if (Guid.TryParseExact(valueOrNull, "D", out value) == false || value == Guid.Empty)
        {
            throw invalidResponse();
        }

        return new PlanId(value);
    }

    private sealed class ValidatedApplyResponse
    {
        public string CalendarIdentifier { get; }

        public PlanName CalendarName { get; }

        public string SourceIdentifier { get; }

        public IReadOnlyList<AppleCalendarManagedEventRegistration> Events { get; }

        public ValidatedApplyResponse(string calendarIdentifier, PlanName calendarName, string sourceIdentifier, IReadOnlyList<AppleCalendarManagedEventRegistration> events)
        {
            CalendarIdentifier = calendarIdentifier;
            CalendarName = calendarName;
            SourceIdentifier = sourceIdentifier;
            Events = events;
        }
    }

    private enum EReconciliationNotFoundKind
    {
        None = 0,
        OperationNotCommitted = 1,
        CalendarMissing = 2,
    }
}
