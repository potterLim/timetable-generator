using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class EventKitAppleCalendarNativeBridgeListingTests
    : EventKitAppleCalendarNativeBridgeTestFixture
{
    [Fact]
    public async Task ListingCombinesExactRegistryAndLegacyOwnershipEvidenceAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "ok",
              "diagnosticCode": "",
              "calendars": [
                {
                  "identifier": "registered-calendar",
                  "name": "2026-2학기 시간표",
                  "sourceIdentifier": "source-a",
                  "writable": true,
                  "registeredPlanId": "71f3be04-d4c6-41d4-a269-792321e71423",
                  "legacyPlanId": "",
                  "legacyManaged": false
                },
                {
                  "identifier": "legacy-calendar",
                  "name": "2026-2학기 시간표",
                  "sourceIdentifier": "source-b",
                  "writable": true,
                  "registeredPlanId": "",
                  "legacyPlanId": "71f3be04-d4c6-41d4-a269-792321e71423",
                  "legacyManaged": true
                }
              ]
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        IReadOnlyList<AppleCalendarDescriptor> calendars = await bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken);

        Assert.Equal(2, calendars.Count);
        Assert.All(calendars, calendar => Assert.Equal(PLAN_ID, calendar.ManagedPlanIdOrNull));
        Assert.All(calendars, calendar => Assert.True(calendar.CanReplace));
        using (JsonDocument request = JsonDocument.Parse(Assert.Single(command.Requests)))
        {
            Assert.Equal("list", request.RootElement.GetProperty("operation").GetString());
            Assert.Equal(document.CalendarName.Value, request.RootElement.GetProperty("requestedName").GetString());
            JsonElement registrationRequest = Assert.Single(request.RootElement.GetProperty("registrations").EnumerateArray());
            Assert.Equal("registered-calendar", registrationRequest.GetProperty("calendarIdentifier").GetString());
            Assert.Equal(PLAN_ID.ToString(), registrationRequest.GetProperty("planId").GetString());
            Assert.Equal(document.CalendarName.Value, registrationRequest.GetProperty("calendarName").GetString());
            Assert.Equal("source-a", registrationRequest.GetProperty("sourceIdentifier").GetString());
            Assert.Single(registrationRequest.GetProperty("managedEvents").EnumerateArray());
            assertLegacyMigrationRange(root: request.RootElement);
        }
    }

    [Fact]
    public async Task ListingRebindsAFullSyncIdentifierChangeOnlyAfterCompleteNativeProofAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        AppleCalendarManagedEventRegistration managedEvent = Assert.Single(registration.Events);
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null));
        string response = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                status = "ok",
                diagnosticCode = "",
                calendars = new[]
                {
                    new
                    {
                        identifier = "rebound-calendar",
                        name = document.CalendarName.Value,
                        sourceIdentifier = "source-a",
                        writable = true,
                        registeredPlanId = document.PlanId.ToString(),
                        legacyPlanId = "",
                        legacyManaged = false,
                    },
                },
                registrationBindings = new[]
                {
                    createRegistrationBinding(registration, "rebound-calendar", "rebound-item", "rebound-external"),
                },
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(response);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarDescriptor descriptor = Assert.Single(await bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken));

        Assert.True(descriptor.CanReplace);
        Assert.Equal("rebound-calendar", descriptor.CalendarId.Value);
        AppleCalendarRegistration reboundRegistration = Assert.Single(registryStore.Current.Calendars);
        Assert.Equal("rebound-calendar", reboundRegistration.CalendarIdentifier);
        AppleCalendarManagedEventRegistration reboundEvent = Assert.Single(reboundRegistration.Events);
        Assert.Equal("rebound-item", reboundEvent.CalendarItemIdentifier);
        Assert.Equal("rebound-external", reboundEvent.ExternalIdentifierOrNull);
        Assert.Equal(managedEvent.Fingerprint, reboundEvent.Fingerprint);
        Assert.Single(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task ListingRefreshesEventIdentifiersWhenTheCalendarIdentifierIsStableAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null));
        string response = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                status = "ok",
                diagnosticCode = "",
                calendars = new[]
                {
                    new
                    {
                        identifier = registration.CalendarIdentifier,
                        name = registration.CalendarName,
                        sourceIdentifier = registration.SourceIdentifier,
                        writable = true,
                        registeredPlanId = registration.PlanId,
                        legacyPlanId = "",
                        legacyManaged = false,
                    },
                },
                registrationBindings = new[]
                {
                    createRegistrationBinding(registration, registration.CalendarIdentifier, "refreshed-item", "refreshed-external"),
                },
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(response);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarDescriptor descriptor = Assert.Single(await bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken));

        Assert.True(descriptor.CanReplace);
        Assert.Equal(registration.CalendarIdentifier, descriptor.CalendarId.Value);
        AppleCalendarRegistration refreshedRegistration = Assert.Single(registryStore.Current.Calendars);
        Assert.Equal(registration.CalendarIdentifier, refreshedRegistration.CalendarIdentifier);
        AppleCalendarManagedEventRegistration refreshedEvent = Assert.Single(refreshedRegistration.Events);
        Assert.Equal("refreshed-item", refreshedEvent.CalendarItemIdentifier);
        Assert.Equal("refreshed-external", refreshedEvent.ExternalIdentifierOrNull);
        Assert.Single(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task SameCalendarIdentifierBindingRequiresTheOriginalEventFingerprintAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null));
        Dictionary<string, object?> binding = createRegistrationBinding(registration, registration.CalendarIdentifier, "refreshed-item", "refreshed-external");
        binding["events"] = new object[]
        {
            new
            {
                sourceEventHash = Assert.Single(registration.Events).SourceEventHash,
                calendarItemIdentifier = "refreshed-item",
                externalIdentifier = "refreshed-external",
                fingerprint = new string('f', 64),
            },
        };
        string response = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                status = "ok",
                diagnosticCode = "",
                calendars = new[]
                {
                    new
                    {
                        identifier = registration.CalendarIdentifier,
                        name = registration.CalendarName,
                        sourceIdentifier = registration.SourceIdentifier,
                        writable = true,
                        registeredPlanId = registration.PlanId,
                        legacyPlanId = "",
                        legacyManaged = false,
                    },
                },
                registrationBindings = new[] { binding },
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(response);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_invalid_native_response", exception.DiagnosticCode);
        Assert.Same(registration, Assert.Single(registryStore.Current.Calendars));
        Assert.Empty(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task ListingRemovesAStaleRegistrationAfterItsCalendarWasDeletedAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "deleted-calendar", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null));
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "ok",
              "diagnosticCode": "",
              "calendars": []
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        IReadOnlyList<AppleCalendarDescriptor> calendars = await bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken);

        Assert.Empty(calendars);
        Assert.Empty(registryStore.Current.Calendars);
        Assert.Null(registryStore.Current.PendingOperation);
        Assert.Single(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task ListingRejectsAnUnprovenCandidateForAMissingRegistrationAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "missing-calendar", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null));
        string response = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                status = "ok",
                diagnosticCode = "",
                calendars = new[]
                {
                    new
                    {
                        identifier = "unproven-calendar",
                        name = registration.CalendarName,
                        sourceIdentifier = registration.SourceIdentifier,
                        writable = true,
                        registeredPlanId = "",
                        legacyPlanId = "",
                        legacyManaged = false,
                    },
                },
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(response);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_invalid_native_response", exception.DiagnosticCode);
        Assert.Same(registration, Assert.Single(registryStore.Current.Calendars));
        Assert.Empty(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task StaleRegistrationCleanupFailureLeavesTheRegistryUnchangedAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "deleted-calendar", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null))
        {
            FailureOnSaveAttemptOrNull = 1,
        };
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(
            """
            {
              "schemaVersion": 1,
              "status": "ok",
              "diagnosticCode": "",
              "calendars": []
            }
            """);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_registry_cleanup_failed", exception.DiagnosticCode);
        Assert.Same(registration, Assert.Single(registryStore.Current.Calendars));
        Assert.Empty(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task IncompleteListRebindingProofLeavesRegistryUnchangedAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null));
        Dictionary<string, object?> incompleteBinding = createRegistrationBinding(registration, "rebound-calendar", "rebound-item", "rebound-external");
        incompleteBinding["events"] = Array.Empty<object>();
        string response = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                status = "ok",
                diagnosticCode = "",
                calendars = new[]
                {
                    new
                    {
                        identifier = "rebound-calendar",
                        name = document.CalendarName.Value,
                        sourceIdentifier = "source-a",
                        writable = true,
                        registeredPlanId = document.PlanId.ToString(),
                        legacyPlanId = "",
                        legacyManaged = false,
                    },
                },
                registrationBindings = new[] { incompleteBinding },
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(response);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_invalid_native_response", exception.DiagnosticCode);
        Assert.Same(registration, Assert.Single(registryStore.Current.Calendars));
        Assert.Empty(registryStore.SavedDocuments);
    }

    [Fact]
    public async Task RebindingSaveFailureDoesNotExposeAReplaceableCalendarAsync()
    {
        CalendarExportDocument document = createDocument();
        AppleCalendarRegistration registration = createRegistration(document, "registered-calendar", "source-a");
        RecordingRegistryStore registryStore = new RecordingRegistryStore(new AppleCalendarOwnershipRegistryDocument(AppleCalendarOwnershipRegistryDocument.CURRENT_SCHEMA_VERSION, new AppleCalendarRegistration[] { registration }, null))
        {
            FailureOnSaveAttemptOrNull = 1,
        };
        string response = JsonSerializer.Serialize(
            new
            {
                schemaVersion = 1,
                status = "ok",
                diagnosticCode = "",
                calendars = new[]
                {
                    new
                    {
                        identifier = "rebound-calendar",
                        name = document.CalendarName.Value,
                        sourceIdentifier = "source-a",
                        writable = true,
                        registeredPlanId = document.PlanId.ToString(),
                        legacyPlanId = "",
                        legacyManaged = false,
                    },
                },
                registrationBindings = new[]
                {
                    createRegistrationBinding(registration, "rebound-calendar", "rebound-item", "rebound-external"),
                },
            });
        RecordingEventKitCalendarCommand command = new RecordingEventKitCalendarCommand(response);
        EventKitAppleCalendarNativeBridge bridge = new EventKitAppleCalendarNativeBridge(command, registryStore);

        AppleCalendarNativeBridgeException exception = await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
            () => bridge.GetCalendarsAsync(document, TestContext.Current.CancellationToken));

        Assert.Equal("apple_calendar_registry_rebind_failed", exception.DiagnosticCode);
        Assert.Same(registration, Assert.Single(registryStore.Current.Calendars));
        Assert.Empty(registryStore.SavedDocuments);
    }

}
