using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class EventKitAppleCalendarNativeBridgeMutationTests
    : EventKitAppleCalendarNativeBridgeTestFixture
{
    [Fact]
    public async Task CreatePersistsPendingBeforeNativeMutationAndRegistersIdentifiersAfterSuccessAsync()
    {
        CalendarExportDocument document = createDocument();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty());
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(requestJson =>
        {
            Assert.NotNull(registryStore.Current.PendingOperationOrNull);
            return createSuccessfulResponse(requestJson, "created-calendar", "source-a", 0);
        });
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeExportResult result = await bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken);

        Assert.Equal("created-calendar", result.CalendarId.Value);
        Assert.Equal(1, result.CreatedEventCount);
        Assert.Equal(2, registryStore.SavedDocuments.Count);
        Assert.NotNull(registryStore.SavedDocuments[0].PendingOperationOrNull);
        Assert.Null(registryStore.Current.PendingOperationOrNull);
        AppleCalendarRegistration completedRegistration = Assert.Single(registryStore.Current.Calendars);
        Assert.Equal("created-calendar", completedRegistration.CalendarIdentifier);
        Assert.Equal("source-a", completedRegistration.SourceIdentifier);
        Assert.Single(completedRegistration.Events);
        using (JsonDocument request = JsonDocument.Parse(Assert.Single(command.Requests)))
        {
            JsonElement root = request.RootElement;
            Assert.Equal("apply", root.GetProperty("operation").GetString());
            Assert.Equal("create", root.GetProperty("mutationKind").GetString());
            Assert.Equal(string.Empty, root.GetProperty("registeredPlanId").GetString());
            JsonElement recurringEvent = Assert.Single(root.GetProperty("recurringEvents").EnumerateArray());
            Assert.False(recurringEvent.TryGetProperty("url", out _));
            Assert.False(recurringEvent.TryGetProperty("ownershipUrl", out _));
            Assert.False(root.TryGetProperty("ownershipMarkerPrefix", out _));
            assertLegacyMigrationRange(root);
        }
    }

    [Fact]
    public async Task RequestAndPendingRegistryUseTheSameAsciiOnlyUnicodeCanonicalNameAsync()
    {
        CalendarExportDocument document = createDocument(new PlanName("Straße timetable"));
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty());
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(requestJson => createSuccessfulResponse(requestJson, "created-calendar", "source-a", 0));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        await bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken);

        using (JsonDocument request = JsonDocument.Parse(Assert.Single(command.Requests)))
        {
            Assert.Equal("STRAßE TIMETABLE", request.RootElement.GetProperty("normalizedDestinationName").GetString());
            Assert.Equal("STRAßE TIMETABLE", registryStore.SavedDocuments[0].PendingOperationOrNull!.NormalizedCalendarName);
            Assert.Equal("STRAßE TIMETABLE", Assert.Single(registryStore.Current.Calendars).NormalizedCalendarName);
        }
    }

    [Fact]
    public async Task ReplacementPassesExactRegistryPreconditionsAndManagedEventIdentifiersAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(requestJson => createSuccessfulResponse(requestJson, "registered-calendar", "source-a", 1));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);
        AppleCalendarExportMutation mutation = AppleCalendarExportMutation.ReplaceExisting(document, document.CalendarName, new AppleCalendarId("registered-calendar"), "source-a", PLAN_ID);

        AppleCalendarNativeExportResult result = await bridge.ApplyExportAsync(mutation, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.DeletedEventCount);
        using (JsonDocument request = JsonDocument.Parse(Assert.Single(command.Requests)))
        {
            JsonElement root = request.RootElement;
            Assert.Equal("replace", root.GetProperty("mutationKind").GetString());
            Assert.Equal("registered-calendar", root.GetProperty("existingCalendarIdentifier").GetString());
            Assert.Equal("source-a", root.GetProperty("expectedSourceIdentifier").GetString());
            Assert.Equal(PLAN_ID.ToString(), root.GetProperty("registeredPlanId").GetString());
            JsonElement managedEvent = Assert.Single(root.GetProperty("managedEvents").EnumerateArray());
            Assert.Equal("old-item", managedEvent.GetProperty("calendarItemIdentifier").GetString());
            Assert.Equal("old-external", managedEvent.GetProperty("externalIdentifier").GetString());
        }
    }

    [Fact]
    public async Task NativeFailureLeavesPendingRegistryForFailClosedRecoveryAsync()
    {
        CalendarExportDocument document = createDocument();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty());
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "operation_failed",
              "diagnosticCode": "eventkit_calendar_commit_failed"
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken));

        Assert.Equal(EAppleCalendarNativeFailureKind.OperationFailed, exception.FailureKind);
        Assert.Equal("eventkit_calendar_commit_failed", exception.DiagnosticCode);
        Assert.NotNull(registryStore.Current.PendingOperationOrNull);
        Assert.Single(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task PrecommitCalendarChangeClearsPendingStateBeforeRetryAsync()
    {
        CalendarExportDocument document = createDocument();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty());
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "calendar_changed",
              "diagnosticCode": "eventkit_calendar_destination_changed"
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken));

        Assert.Equal(EAppleCalendarNativeFailureKind.CalendarChanged, exception.FailureKind);
        Assert.Null(registryStore.Current.PendingOperationOrNull);
        Assert.Equal(2, registryStore.SavedDocuments.Count);
    }

    [Theory]
    [InlineData("access_denied", "eventkit_calendar_access_denied", false)]
    [InlineData("invalid_request", "eventkit_request_destination_name_invalid", false)]
    [InlineData("invalid_request", "eventkit_request_migration_range_invalid", false)]
    [InlineData("invalid_request", "eventkit_request_reconciliation_precondition_invalid", true)]
    [InlineData("calendar_changed", "eventkit_future_calendar_change", true)]
    public async Task PendingCleanupRequiresAnExactKnownPrecommitResponseAsync(string status, string diagnosticCode, bool expectedPending)
    {
        CalendarExportDocument document = createDocument();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty());
        string response = JsonSerializer.Serialize(new { schemaVersion = 1, status, diagnosticCode });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(response);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken));

        Assert.Equal(expectedPending, registryStore.Current.PendingOperationOrNull != null);
        int expectedDocumentCount;
        if (expectedPending)
        {
            expectedDocumentCount = 1;
        }
        else
        {
            expectedDocumentCount = 2;
        }
        Assert.Equal(expectedDocumentCount, registryStore.SavedDocuments.Count);
    }

    [Fact]
    public async Task ExportServiceCanReloadAfterAnActualBridgeDestinationRaceAsync()
    {
        CalendarExportDocument document = createDocument();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty());
        Queue<string> responses = new Queue<string>(
            new[]
            {
                """
                {
                  "schemaVersion": 1,
                  "status": "ok",
                  "diagnosticCode": "",
                  "calendars": []
                }
                """,
                """
                {
                  "schemaVersion": 1,
                  "status": "calendar_changed",
                  "diagnosticCode": "eventkit_calendar_destination_changed"
                }
                """,
                """
                {
                  "schemaVersion": 1,
                  "status": "ok",
                  "diagnosticCode": "",
                  "calendars": [
                    {
                      "identifier": "racing-calendar",
                      "name": "2026-2학기 시간표",
                      "sourceIdentifier": "source-race",
                      "writable": true,
                      "registeredPlanId": "",
                      "legacyPlanId": "",
                      "legacyManaged": false
                    }
                  ]
                }
                """,
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(_ => responses.Dequeue());
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);
        AppleCalendarExportService service = new AppleCalendarExportService(bridge);

        AppleCalendarExportResult result = await service.ExportAsync(document, new FixedCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel), TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Cancelled, result.Status);
        Assert.Null(registryStore.Current.PendingOperationOrNull);
        Assert.Empty(responses);
        Assert.Collection(
            command.Requests,
            request => Assert.Equal("list", getOperation(request)),
            request => Assert.Equal("apply", getOperation(request)),
            request => Assert.Equal("list", getOperation(request)));
    }

    [Fact]
    public async Task AmbiguousRegistrationStillProvidesASafeNameConflictSnapshotAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration firstRegistration = createRegistration(document, "registered-calendar-one", "source-a");
        AppleCalendarRegistration secondRegistration = createRegistration(document, "registered-calendar-two", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(
            new AppleCalendarOwnershipRegistryDocument(
                AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION,
                new AppleCalendarRegistration[] { firstRegistration, secondRegistration },
                null));
        Queue<string> responses = new Queue<string>(
            new[]
            {
                """
                {
                  "schemaVersion": 1,
                  "status": "calendar_changed",
                  "diagnosticCode": "eventkit_calendar_registration_ambiguous"
                }
                """,
                JsonSerializer.Serialize(
                    new
                    {
                        schemaVersion = 1,
                        status = "ok",
                        diagnosticCode = "",
                        calendars = new[]
                        {
                            new
                            {
                                identifier = "current-calendar-one",
                                name = document.CalendarName.Value,
                                sourceIdentifier = "source-a",
                                writable = true,
                                registeredPlanId = "",
                                legacyPlanId = "",
                                legacyManaged = false,
                            },
                            new
                            {
                                identifier = "current-calendar-two",
                                name = document.CalendarName.Value,
                                sourceIdentifier = "source-a",
                                writable = true,
                                registeredPlanId = "",
                                legacyPlanId = "",
                                legacyManaged = false,
                            },
                        },
                    }),
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(_ => responses.Dequeue());
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);
        FixedCalendarNameConflictResolver conflictResolver = new FixedCalendarNameConflictResolver(ECalendarNameConflictResolution.Cancel);

        AppleCalendarExportResult result = await new AppleCalendarExportService(bridge).ExportAsync(document, conflictResolver, TestContext.Current.CancellationToken);

        Assert.Equal(EAppleCalendarExportStatus.Cancelled, result.Status);
        CalendarNameConflict conflict = Assert.Single(conflictResolver.Conflicts);
        Assert.False(conflict.CanReplace);
        Assert.Equal("2026-2학기 시간표 (2)", conflict.NextAvailableName.Value);
        Assert.Empty(responses);
        Assert.Empty(registryStore.SavedDocuments);
        Assert.Equal(2, registryStore.Current.Calendars.Count);
        Assert.Collection(
            command.Requests,
            request => Assert.Equal(2, getRegistrationCount(request)),
            request => Assert.Equal(0, getRegistrationCount(request)));
    }

    [Fact]
    public async Task RegistryLoadFailureStopsBeforeNativeAccessWithAnExactDiagnosticAsync()
    {
        CalendarExportDocument document = createDocument();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty())
        {
            FailureOnLoadOrNull = new System.IO.IOException("Controlled registry load failure."),
        };
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(_ => throw new InvalidOperationException("Native access must not run."));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_registry_load_failed", exception.DiagnosticCode);
        Assert.Empty(command.Requests);
        Assert.Empty(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task PendingRegistryPreparationFailureStopsBeforeNativeMutationAsync()
    {
        CalendarExportDocument document = createDocument();
        RecordingRegistryStore registryStore = new RecordingRegistryStore(AppleCalendarOwnershipRegistryDocument.CreateEmpty())
        {
            FailureOnSaveAttemptOrNull = 1,
        };
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(_ => throw new InvalidOperationException("Native mutation must not run."));
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.ApplyExportAsync(AppleCalendarExportMutation.CreateNew(document, document.CalendarName), TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_registry_prepare_failed", exception.DiagnosticCode);
        Assert.Empty(command.Requests);
        Assert.Empty(registryStore.SavedDocuments);
    }

}
