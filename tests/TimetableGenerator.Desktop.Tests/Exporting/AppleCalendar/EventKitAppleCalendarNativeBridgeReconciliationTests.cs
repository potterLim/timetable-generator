using System;
using System.Text.Json;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class EventKitAppleCalendarNativeBridgeReconciliationTests
    : EventKitAppleCalendarNativeBridgeTestFixture
{
    [Fact]
    public async Task ReconciliationFinalizesNativeSuccessAfterRegistrySaveFailureWithoutMutatingAgainAsync()
    {
        CalendarExportDocument document = createDocument();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty())
        {
            FailureOnSaveAttemptOrNull = 2,
        };
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(requestJson => createSuccessfulResponse(requestJson, "created-calendar", "source-a", 0));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException saveException = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_registry_finalize_failed", saveException.DiagnosticCode);
        Assert.NotNull(registryStore.Current.PendingOperationOrNull);
        registryStore.FailureOnSaveAttemptOrNull = null;

        await bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken);

        Assert.Null(registryStore.Current.PendingOperationOrNull);
        Assert.Single(registryStore.Current.Calendars);
        Assert.Collection(
            command.Requests,
            request => Assert.Equal("apply", getOperation(request)),
            request =>
            {
                using (JsonDocument reconciliationRequest = JsonDocument.Parse(request))
                {
                    JsonElement root = reconciliationRequest.RootElement;
                    Assert.Equal("reconcile", root.GetProperty("operation").GetString());
                    Assert.Empty(root.GetProperty("recurringEvents").EnumerateArray());
                    Assert.Single(root.GetProperty("desiredEvents").EnumerateArray());
                    Assert.True(root.GetProperty("preparedAtUnixSeconds").GetInt64() > 0);
                    assertLegacyMigrationRange(root);
                }
            });
    }

    [Fact]
    public void LegacyMigrationRangeAddsOneLeapYearAndClampsWithoutOverflow()
    {
        const long MIGRATION_PADDING_SECONDS = 366L * 24L * 60L * 60L;

        (long normalStart, long normalEnd) = EventKitAppleCalendarRequest.getLegacyMigrationRange(100, 200);
        (long clampedStart, long lowerEnd) = EventKitAppleCalendarRequest.getLegacyMigrationRange(long.MinValue + 1, long.MinValue + 1);
        (long upperStart, long clampedEnd) = EventKitAppleCalendarRequest.getLegacyMigrationRange(long.MaxValue - 2, long.MaxValue - 2);

        Assert.Equal(100 - MIGRATION_PADDING_SECONDS, normalStart);
        Assert.Equal(200 + MIGRATION_PADDING_SECONDS, normalEnd);
        Assert.Equal(long.MinValue, clampedStart);
        Assert.Equal(long.MinValue + 1 + MIGRATION_PADDING_SECONDS, lowerEnd);
        Assert.Equal(long.MaxValue - 2 - MIGRATION_PADDING_SECONDS, upperStart);
        Assert.Equal(long.MaxValue - 1, clampedEnd);
    }

    [Fact]
    public async Task ReconciliationNotFoundClearsPendingBeforeFreshExportAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));
        (long termStart, long termEnd) = EventKitAppleCalendarRequest.GetTermRange(document);
        AppleCalendarPendingOperation pendingOperation = new AppleCalendarPendingOperation(
            Guid.NewGuid().ToString("D"),
            document.PlanId.ToString(),
            document.PlanId.ToString(),
            null,
            null,
            document.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(document.CalendarName.Value),
            termStart,
            termEnd,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            new AppleCalendarPendingEvent[]
            {
                new AppleCalendarPendingEvent(recurringEvent.SourceEventHash, recurringEvent.Fingerprint),
            });
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, Array.Empty<AppleCalendarRegistration>(), pendingOperation));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "not_found",
              "diagnosticCode": "eventkit_reconciliation_not_found"
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        await bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken);

        Assert.Null(registryStore.Current.PendingOperationOrNull);
        Assert.Empty(registryStore.Current.Calendars);
        Assert.Single(registryStore.SavedDocuments);
        Assert.Equal("reconcile", getOperation(Assert.Single(command.Requests)));
    }

    [Fact]
    public async Task PendingCleanupFailureUsesALocalStateDiagnosticAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));
        (long termStart, long termEnd) = EventKitAppleCalendarRequest.GetTermRange(document);
        AppleCalendarPendingOperation pendingOperation = new AppleCalendarPendingOperation(
            Guid.NewGuid().ToString("D"),
            document.PlanId.ToString(),
            document.PlanId.ToString(),
            null,
            null,
            document.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(document.CalendarName.Value),
            termStart,
            termEnd,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            new AppleCalendarPendingEvent[]
            {
                new AppleCalendarPendingEvent(recurringEvent.SourceEventHash, recurringEvent.Fingerprint),
            });
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, Array.Empty<AppleCalendarRegistration>(), pendingOperation))
        {
            FailureOnSaveAttemptOrNull = 1,
        };
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "not_found",
              "diagnosticCode": "eventkit_reconciliation_not_found"
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_registry_cleanup_failed", exception.DiagnosticCode);
        Assert.NotNull(registryStore.Current.PendingOperationOrNull);
        Assert.Single(command.Requests);
    }

    [Fact]
    public async Task MissingReplacementCalendarPreservesRegistrationAndPendingOperationAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        AppleCalendarRegistration otherRegistration = createRegistration(document, "other-calendar", "source-b");
        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));
        (long termStart, long termEnd) = EventKitAppleCalendarRequest.GetTermRange(document);
        AppleCalendarPendingOperation pendingOperation = new AppleCalendarPendingOperation(
            Guid.NewGuid().ToString("D"),
            document.PlanId.ToString(),
            document.PlanId.ToString(),
            registration.CalendarIdentifier,
            registration.SourceIdentifier,
            document.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(document.CalendarName.Value),
            termStart,
            termEnd,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            new AppleCalendarPendingEvent[]
            {
                new AppleCalendarPendingEvent(recurringEvent.SourceEventHash, recurringEvent.Fingerprint),
            });
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration, otherRegistration }, pendingOperation));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "not_found",
              "diagnosticCode": "eventkit_reconciliation_calendar_not_found"
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_registered_identifier_unavailable", exception.DiagnosticCode);
        Assert.Same(pendingOperation, registryStore.Current.PendingOperationOrNull);
        Assert.Equal(new AppleCalendarRegistration[] { otherRegistration, registration }, registryStore.Current.Calendars);
        Assert.Empty(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task UncommittedReplacementKeepsItsRegistrationWhileClearingPendingOperationAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));
        (long termStart, long termEnd) = EventKitAppleCalendarRequest.GetTermRange(document);
        AppleCalendarPendingOperation pendingOperation = new AppleCalendarPendingOperation(
            Guid.NewGuid().ToString("D"),
            document.PlanId.ToString(),
            document.PlanId.ToString(),
            registration.CalendarIdentifier,
            registration.SourceIdentifier,
            document.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(document.CalendarName.Value),
            termStart,
            termEnd,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            new AppleCalendarPendingEvent[]
            {
                new AppleCalendarPendingEvent(recurringEvent.SourceEventHash, recurringEvent.Fingerprint),
            });
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, pendingOperation));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "not_found",
              "diagnosticCode": "eventkit_reconciliation_not_found"
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        await bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken);

        Assert.Null(registryStore.Current.PendingOperationOrNull);
        Assert.Same(registration, Assert.Single(registryStore.Current.Calendars));
        Assert.Single(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task UncommittedReplacementRebindsFullSyncIdentifiersBeforeClearingPendingAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        AppleCalendarPendingOperation pendingOperation = createPendingReplacement(document, registration);
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, pendingOperation));
        string response = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                status = "not_found",
                diagnosticCode = "eventkit_reconciliation_not_found",
                registrationBindings = new[]
                {
                    createRegistrationBinding(registration, "rebound-calendar", "rebound-item", "rebound-external"),
                },
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(response);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        await bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken);

        Assert.Null(registryStore.Current.PendingOperationOrNull);
        AppleCalendarRegistration reboundRegistration = Assert.Single(registryStore.Current.Calendars);
        Assert.Equal("rebound-calendar", reboundRegistration.CalendarIdentifier);
        Assert.Equal("rebound-item", Assert.Single(reboundRegistration.Events).CalendarItemIdentifier);
        Assert.Equal(2, registryStore.SavedDocuments.Count);
        Assert.Equal("rebound-calendar", registryStore.SavedDocuments[0].PendingOperationOrNull!.CalendarIdentifierOrNull);
    }

    [Fact]
    public async Task UncommittedReplacementRefreshesEventIdentifiersWhenTheCalendarIdentifierIsStableAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        AppleCalendarPendingOperation pendingOperation = createPendingReplacement(document, registration);
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, pendingOperation));
        string response = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                status = "not_found",
                diagnosticCode = "eventkit_reconciliation_not_found",
                registrationBindings = new[]
                {
                    createRegistrationBinding(registration, registration.CalendarIdentifier, "refreshed-item", "refreshed-external"),
                },
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(response);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        await bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken);

        Assert.Null(registryStore.Current.PendingOperationOrNull);
        AppleCalendarRegistration refreshedRegistration = Assert.Single(registryStore.Current.Calendars);
        Assert.Equal(registration.CalendarIdentifier, refreshedRegistration.CalendarIdentifier);
        Assert.Equal("refreshed-item", Assert.Single(refreshedRegistration.Events).CalendarItemIdentifier);
        Assert.Equal(2, registryStore.SavedDocuments.Count);
        Assert.Equal(registration.CalendarIdentifier, registryStore.SavedDocuments[0].PendingOperationOrNull!.CalendarIdentifierOrNull);
    }

    [Fact]
    public async Task CommittedReplacementRebindsChangedCalendarIdentifierFromProvenDesiredStateAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        AppleCalendarPendingOperation pendingOperation = createPendingReplacement(document, registration);
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, pendingOperation));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(requestJson => createSuccessfulResponse(requestJson, "rebound-calendar", "source-a", 0));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        await bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken);

        Assert.Null(registryStore.Current.PendingOperationOrNull);
        AppleCalendarRegistration reboundRegistration = Assert.Single(registryStore.Current.Calendars);
        Assert.Equal("rebound-calendar", reboundRegistration.CalendarIdentifier);
        Assert.Equal("new-item", Assert.Single(reboundRegistration.Events).CalendarItemIdentifier);
        Assert.Single(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task LegacyReconciliationUsesPendingCalendarSourceWithoutARegistrationAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));
        (long termStart, long termEnd) = EventKitAppleCalendarRequest.GetTermRange(document);
        AppleCalendarPendingOperation pendingOperation = new AppleCalendarPendingOperation(
            Guid.NewGuid().ToString("D"),
            document.PlanId.ToString(),
            document.PlanId.ToString(),
            "legacy-calendar",
            "legacy-source",
            document.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(document.CalendarName.Value),
            termStart,
            termEnd,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            new AppleCalendarPendingEvent[]
            {
                new AppleCalendarPendingEvent(recurringEvent.SourceEventHash, recurringEvent.Fingerprint),
            });
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, Array.Empty<AppleCalendarRegistration>(), pendingOperation));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(requestJson => createSuccessfulResponse(requestJson, "legacy-calendar", "legacy-source", 0));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        await bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken);

        AppleCalendarRegistration registration = Assert.Single(registryStore.Current.Calendars);
        Assert.Equal("legacy-source", registration.SourceIdentifier);
        using (JsonDocument request = JsonDocument.Parse(Assert.Single(command.Requests)))
        {
            JsonElement root = request.RootElement;
            Assert.Equal("replace", root.GetProperty("mutationKind").GetString());
            Assert.Equal("legacy-calendar", root.GetProperty("existingCalendarIdentifier").GetString());
            Assert.Equal("legacy-source", root.GetProperty("expectedSourceIdentifier").GetString());
            Assert.Equal(string.Empty, root.GetProperty("registeredPlanId").GetString());
            Assert.Empty(root.GetProperty("managedEvents").EnumerateArray());
        }
    }

    [Fact]
    public async Task ReconciliationRejectsAResponseFromAnotherCalendarSourceAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));
        (long termStart, long termEnd) = EventKitAppleCalendarRequest.GetTermRange(document);
        AppleCalendarPendingOperation pendingOperation = new AppleCalendarPendingOperation(
            Guid.NewGuid().ToString("D"),
            document.PlanId.ToString(),
            document.PlanId.ToString(),
            "legacy-calendar",
            "legacy-source",
            document.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(document.CalendarName.Value),
            termStart,
            termEnd,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            new AppleCalendarPendingEvent[]
            {
                new AppleCalendarPendingEvent(recurringEvent.SourceEventHash, recurringEvent.Fingerprint),
            });
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, Array.Empty<AppleCalendarRegistration>(), pendingOperation));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(requestJson => createSuccessfulResponse(requestJson, "legacy-calendar", "different-source", 0));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ReconcilePendingOperationAsync(TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_invalid_native_response", exception.DiagnosticCode);
        Assert.NotNull(registryStore.Current.PendingOperationOrNull);
        Assert.Empty(registryStore.Current.Calendars);
        Assert.Empty(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task ConflictingPendingOperationStopsBeforeNativeMutationAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarPendingOperation pendingOperation = new AppleCalendarPendingOperation(
            Guid.NewGuid().ToString("D"),
            PlanId.CreateNew().ToString(),
            document.PlanId.ToString(),
            null,
            null,
            document.CalendarName.Value,
            EventKitAppleCalendarRequest.NormalizeCalendarName(document.CalendarName.Value),
            EventKitAppleCalendarRequest.GetTermRange(document).StartsAtUnixSeconds,
            EventKitAppleCalendarRequest.GetTermRange(document).EndsAtUnixSeconds,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            new AppleCalendarPendingEvent[]
            {
                new AppleCalendarPendingEvent(new string('a', 64), new string('b', 64)),
            });
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, Array.Empty<AppleCalendarRegistration>(), pendingOperation));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(_ => throw new InvalidOperationException("Native mutation must not run."));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_pending_operation_conflict", exception.DiagnosticCode);
        Assert.Empty(command.Requests);
        Assert.Empty(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task DuplicateEventFingerprintsStopBeforePendingStateOrNativeMutationAsync()
    {
        CalendarExportDocument document = createDocumentWithDuplicateEventFingerprints();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty());
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(_ => throw new InvalidOperationException("Native mutation must not run."));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_event_identity_ambiguous", exception.DiagnosticCode);
        Assert.Empty(command.Requests);
        Assert.Empty(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task InvalidNativeEventIdentityLeavesPendingRegistryAsync()
    {
        CalendarExportDocument document = createDocument();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty());
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "ok",
              "diagnosticCode": "",
              "calendarIdentifier": "created-calendar",
              "calendarName": "2026-2학기 시간표",
              "sourceIdentifier": "source-a",
              "createdEventCount": 1,
              "deletedEventCount": 0,
              "events": [
                {
                  "sourceEventHash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "calendarItemIdentifier": "unexpected-item",
                  "externalIdentifier": "",
                  "fingerprint": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                }
              ]
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_invalid_native_response", exception.DiagnosticCode);
        Assert.NotNull(registryStore.Current.PendingOperationOrNull);
        Assert.Single(registryStore.SavedDocuments);
    }

}
