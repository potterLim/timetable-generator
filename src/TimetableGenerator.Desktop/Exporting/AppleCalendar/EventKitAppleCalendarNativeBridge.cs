using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed partial class EventKitAppleCalendarNativeBridge : IAppleCalendarNativeBridge
{
    private const string REGISTRATION_AMBIGUOUS_DIAGNOSTIC_CODE = "eventkit_calendar_registration_ambiguous";

    private static readonly JsonSerializerOptions JSON_OPTIONS = createJsonOptions();

    private readonly IEventKitCalendarCommand mCommand;

    private readonly IAppleCalendarOwnershipRegistryStore mRegistryStore;

    public bool IsAvailable
    {
        get
        {
            return mCommand.IsAvailable;
        }
    }

    public EventKitAppleCalendarNativeBridge(IEventKitCalendarCommand command, IAppleCalendarOwnershipRegistryStore registryStore)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (registryStore == null)
        {
            throw new ArgumentNullException(nameof(registryStore));
        }

        mCommand = command;
        mRegistryStore = registryStore;
    }

    public async Task ReconcilePendingOperationAsync(CancellationToken cancellationToken)
    {
        ensureAvailable();
        AppleCalendarOwnershipRegistryDocument registry = loadRegistry();
        AppleCalendarPendingOperation? pendingOperationOrNull = registry.PendingOperationOrNull;
        if (pendingOperationOrNull == null)
        {
            return;
        }

        AppleCalendarRegistration? existingRegistrationOrNull = findExistingRegistrationOrNull(registry, pendingOperationOrNull.CalendarIdentifierOrNull);
        validatePendingRegistration(existingRegistrationOrNull, pendingOperationOrNull);
        string expectedSourceIdentifier;
        if (existingRegistrationOrNull != null)
        {
            expectedSourceIdentifier = existingRegistrationOrNull.SourceIdentifier;
        }
        else if (pendingOperationOrNull.ExpectedSourceIdentifierOrNull != null)
        {
            expectedSourceIdentifier = pendingOperationOrNull.ExpectedSourceIdentifierOrNull;
        }
        else
        {
            expectedSourceIdentifier = string.Empty;
        }

        string registeredPlanId;
        IReadOnlyList<AppleCalendarManagedEventRegistration> managedEvents;
        if (existingRegistrationOrNull != null)
        {
            registeredPlanId = existingRegistrationOrNull.PlanId;
            managedEvents = existingRegistrationOrNull.Events;
        }
        else
        {
            registeredPlanId = string.Empty;
            managedEvents = Array.Empty<AppleCalendarManagedEventRegistration>();
        }

        EventKitAppleCalendarRequest request = EventKitAppleCalendarRequest.CreateReconcile(
            pendingOperationOrNull,
            expectedSourceIdentifier,
            registeredPlanId,
            managedEvents);
        EventKitAppleCalendarResponse response = await executeAsync(request, cancellationToken).ConfigureAwait(false);
        registry = applyRegistrationBindings(response, registry);
        pendingOperationOrNull = registry.PendingOperationOrNull;
        if (pendingOperationOrNull == null)
        {
            throw invalidResponse();
        }

        existingRegistrationOrNull = findExistingRegistrationOrNull(registry, pendingOperationOrNull.CalendarIdentifierOrNull);
        EReconciliationNotFoundKind notFoundKind = getReconciliationNotFoundKind(response);
        if (notFoundKind != EReconciliationNotFoundKind.None)
        {
            if (notFoundKind == EReconciliationNotFoundKind.CalendarMissing)
            {
                throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.CalendarChanged, "apple_calendar_registered_identifier_unavailable");
            }

            saveRegistry(registry.ClearPendingOperation(), "apple_calendar_registry_cleanup_failed");
            return;
        }

        ensureSuccessfulResponse(response);
        ValidatedApplyResponse validatedResponse = validateReconciliationResponse(response, pendingOperationOrNull, existingRegistrationOrNull);
        completeReconciliation(registry, pendingOperationOrNull, validatedResponse);
    }

    public async Task<IReadOnlyList<AppleCalendarDescriptor>> GetCalendarsAsync(CalendarExportDocument document, CancellationToken cancellationToken)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        ensureAvailable();
        AppleCalendarOwnershipRegistryDocument registry = loadRegistry();
        try
        {
            return await getCalendarsAsync(document, registry, cancellationToken).ConfigureAwait(false);
        }
        catch (AppleCalendarNativeBridgeException exception) when (exception.FailureKind == EAppleCalendarNativeFailureKind.CalendarChanged && string.Equals(exception.DiagnosticCode, REGISTRATION_AMBIGUOUS_DIAGNOSTIC_CODE, StringComparison.Ordinal))
        {
            AppleCalendarOwnershipRegistryDocument unownedSnapshotRegistry = AppleCalendarOwnershipRegistryDocument.CreateEmpty();
            return await getCalendarsAsync(document, unownedSnapshotRegistry, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<AppleCalendarDescriptor>> getCalendarsAsync(
        CalendarExportDocument document,
        AppleCalendarOwnershipRegistryDocument registry,
        CancellationToken cancellationToken)
    {
        EventKitAppleCalendarRequest request = EventKitAppleCalendarRequest.CreateList(document, registry);
        EventKitAppleCalendarResponse response = await executeAsync(request, cancellationToken).ConfigureAwait(false);
        ensureSuccessfulResponse(response);
        if (response.Calendars == null)
        {
            throw invalidResponse();
        }

        registry = applyRegistrationBindings(response, registry);
        return createCalendarDescriptors(response, registry);
    }

    public async Task<AppleCalendarNativeExportResult> ApplyExportAsync(AppleCalendarExportMutation mutation, CancellationToken cancellationToken)
    {
        if (mutation == null)
        {
            throw new ArgumentNullException(nameof(mutation));
        }

        ensureAvailable();
        IReadOnlyList<AppleCalendarRecurringEvent> recurringEvents = AppleCalendarRecurringEventProjector.Project(mutation.Document);
        ensureUniqueFingerprints(recurringEvents);
        AppleCalendarOwnershipRegistryDocument registry = loadRegistry();
        AppleCalendarRegistration? existingRegistrationOrNull = findExistingRegistrationOrNull(registry, mutation);
        validateExistingRegistration(existingRegistrationOrNull, mutation);
        AppleCalendarPendingOperation desiredPendingOperation = createPendingOperation(mutation, recurringEvents);
        AppleCalendarOwnershipRegistryDocument registryWithPending = preparePendingOperation(registry, desiredPendingOperation);
        string expectedSourceIdentifier;
        if (existingRegistrationOrNull != null)
        {
            expectedSourceIdentifier = existingRegistrationOrNull.SourceIdentifier;
        }
        else if (mutation.ExpectedSourceIdentifierOrNull != null)
        {
            expectedSourceIdentifier = mutation.ExpectedSourceIdentifierOrNull;
        }
        else
        {
            expectedSourceIdentifier = string.Empty;
        }

        string registeredPlanId;
        IReadOnlyList<AppleCalendarManagedEventRegistration> managedEvents;
        if (existingRegistrationOrNull != null)
        {
            registeredPlanId = existingRegistrationOrNull.PlanId;
            managedEvents = existingRegistrationOrNull.Events;
        }
        else
        {
            registeredPlanId = string.Empty;
            managedEvents = Array.Empty<AppleCalendarManagedEventRegistration>();
        }

        EventKitAppleCalendarRequest request = EventKitAppleCalendarRequest.CreateApply(
            mutation,
            expectedSourceIdentifier,
            registeredPlanId,
            registryWithPending.PendingOperationOrNull!.PreparedAtUnixSeconds,
            recurringEvents,
            managedEvents);
        EventKitAppleCalendarResponse response = await executeAsync(request, cancellationToken).ConfigureAwait(false);
        try
        {
            ensureSuccessfulResponse(response);
        }
        catch (AppleCalendarNativeBridgeException) when (isDefinitelyUncommittedApplyResponse(response))
        {
            saveRegistry(registryWithPending.ClearPendingOperation(), "apple_calendar_registry_cleanup_failed");
            throw;
        }

        ValidatedApplyResponse validatedResponse = validateApplyResponse(response, mutation, recurringEvents, existingRegistrationOrNull);
        return completeOperation(registryWithPending, desiredPendingOperation, mutation, validatedResponse, response);
    }

    private async Task<EventKitAppleCalendarResponse> executeAsync(EventKitAppleCalendarRequest request, CancellationToken cancellationToken)
    {
        try
        {
            string requestJson = JsonSerializer.Serialize(request, JSON_OPTIONS);
            string responseJson = await mCommand.ExecuteAsync(requestJson, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                throw invalidResponse();
            }

            EventKitAppleCalendarResponse? responseOrNull = JsonSerializer.Deserialize<EventKitAppleCalendarResponse>(responseJson, JSON_OPTIONS);
            if (responseOrNull == null)
            {
                throw invalidResponse();
            }

            return responseOrNull;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AppleCalendarNativeBridgeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException || exception is NotSupportedException)
        {
            throw invalidResponse(exception);
        }
        catch (Exception exception)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_native_operation_failed", exception);
        }
    }

    private void ensureAvailable()
    {
        if (IsAvailable == false)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.Unavailable, "apple_calendar_native_bridge_unavailable");
        }
    }

    private AppleCalendarOwnershipRegistryDocument loadRegistry()
    {
        try
        {
            return mRegistryStore.Load();
        }
        catch (Exception exception) when (exception is AppleCalendarOwnershipRegistryException || exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_registry_load_failed", exception);
        }
    }

    private void saveRegistry(AppleCalendarOwnershipRegistryDocument document, string diagnosticCode)
    {
        try
        {
            mRegistryStore.Save(document);
        }
        catch (Exception exception) when (exception is AppleCalendarOwnershipRegistryException || exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, diagnosticCode, exception);
        }
    }

    private static AppleCalendarNativeBridgeException invalidResponse(Exception? innerExceptionOrNull = null)
    {
        return new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_invalid_native_response", innerExceptionOrNull);
    }

    private static JsonSerializerOptions createJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
    }
}
