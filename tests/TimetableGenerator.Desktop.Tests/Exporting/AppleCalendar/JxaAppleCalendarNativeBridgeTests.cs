using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class JxaAppleCalendarNativeBridgeTests
{
    [Fact]
    public async Task CalendarSnapshotUsesDescriptionMarkerAndWritableStateAsync()
    {
        RecordingAppleCalendarAutomationCommand command =
            new RecordingAppleCalendarAutomationCommand(
                """
                {
                  "status": "ok",
                  "calendars": [
                    {
                      "id": "managed:71f3be04-d4c6-41d4-a269-792321e71423:2026-2%ED%95%99%EA%B8%B0%20%EC%8B%9C%EA%B0%84%ED%91%9C",
                      "name": "2026-2학기 시간표",
                      "description": "timetable-generator://managed-calendar/v1/71f3be04-d4c6-41d4-a269-792321e71423",
                      "writable": true
                    },
                    {
                      "id": "personal",
                      "name": "개인",
                      "description": "사용자 캘린더",
                      "writable": false
                    }
                  ]
                }
                """);
        JxaAppleCalendarNativeBridge bridge = new JxaAppleCalendarNativeBridge(command);

        IReadOnlyList<AppleCalendarDescriptor> calendars = await bridge.GetCalendarsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, calendars.Count);
        Assert.Equal(
            "managed:71f3be04-d4c6-41d4-a269-792321e71423:2026-2%ED%95%99%EA%B8%B0%20%EC%8B%9C%EA%B0%84%ED%91%9C",
            calendars[0].CalendarId.Value);
        Assert.Equal(EAppleCalendarOwnership.ApplicationManaged, calendars[0].Ownership);
        Assert.True(calendars[0].CanReplace);
        Assert.Equal(EAppleCalendarOwnership.External, calendars[1].Ownership);
        Assert.Equal(EAppleCalendarContentAccess.ReadOnly, calendars[1].ContentAccess);
        AppleCalendarAutomationInvocation invocation = Assert.Single(command.Invocations);
        Assert.Equal(EAppleCalendarAutomationOperation.ListCalendars, invocation.Operation);
        using (JsonDocument request = JsonDocument.Parse(invocation.RequestJson))
        {
            Assert.Equal(
                AppleCalendarOwnershipMarker.PREFIX,
                request.RootElement
                    .GetProperty("ownershipMarkerPrefix")
                    .GetString());
        }
    }

    [Fact]
    public void NativeCalendarIdentityUsesOwnershipMarkerAndSnapshotOrdinal()
    {
        string script = AppleCalendarAutomationScript.SOURCE;

        Assert.DoesNotContain(
            "calendar.calendarIdentifier()",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "calendar.id()",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "function calendarSnapshotId(calendar, index, markerPrefix)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "? \"external:\" + String(index)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "+ encodeURIComponent(canonicalName(calendarName(calendar)))",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const operationUrl = createOperationCanaryEvent(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "createOperationCanaryEvent(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "findOperationEventProof(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "proof.event.delete()",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "function createCalendarSnapshotIds(calendars, markerPrefix)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"ambiguous:\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains("createOperationEventUrl(", script, StringComparison.Ordinal);
        Assert.Contains(
            "apple_calendar_creation_target_changed",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("rollbackCreatedCalendar(", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateExpandsWeeklyEventsWithOccurrenceSpecificOffsetsAsync()
    {
        RecordingAppleCalendarAutomationCommand command =
            new RecordingAppleCalendarAutomationCommand(
                """
                {
                  "status": "ok",
                  "calendarId": "managed:71f3be04-d4c6-41d4-a269-792321e71423:2026-2%ED%95%99%EA%B8%B0%20%EC%8B%9C%EA%B0%84%ED%91%9C",
                  "calendarName": "2026-2학기 시간표",
                  "createdEventCount": 3,
                  "deletedEventCount": 0
                }
                """);
        JxaAppleCalendarNativeBridge bridge = new JxaAppleCalendarNativeBridge(command);
        CalendarExportDocument document = createDocumentAcrossDstChange();

        AppleCalendarNativeExportResult result = await bridge.ApplyExportAsync(
            AppleCalendarExportMutation.CreateNew(
                document,
                document.CalendarName),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            "managed:71f3be04-d4c6-41d4-a269-792321e71423:2026-2%ED%95%99%EA%B8%B0%20%EC%8B%9C%EA%B0%84%ED%91%9C",
            result.CalendarId.Value);
        Assert.Equal(3, result.CreatedEventCount);
        AppleCalendarAutomationInvocation invocation = Assert.Single(command.Invocations);
        Assert.Equal(EAppleCalendarAutomationOperation.ApplyExport, invocation.Operation);
        using (JsonDocument request = JsonDocument.Parse(invocation.RequestJson))
        {
            JsonElement root = request.RootElement;
            Assert.Equal("create", root.GetProperty("mutationKind").GetString());
            Assert.Equal("2026-2학기 시간표", root.GetProperty("destinationName").GetString());
            Assert.Equal(
                "timetable-generator://managed-calendar/v1/71f3be04-d4c6-41d4-a269-792321e71423",
                root.GetProperty("ownershipDescription").GetString());
            Assert.Equal(
                AppleCalendarEventOwnershipMarker.PREFIX,
                root.GetProperty(
                    "eventOwnershipMarkerPrefix").GetString());
            JsonElement.ArrayEnumerator events = root.GetProperty("events").EnumerateArray();
            Assert.True(events.MoveNext());
            Assert.Equal(
                "personal:lab:2026-03-01",
                events.Current.GetProperty("eventId").GetString());
            string? firstOwnershipUrlOrNull = events.Current
                .GetProperty("ownershipUrl")
                .GetString();
            Assert.True(
                AppleCalendarEventOwnershipMarker.IsApplicationManaged(
                    firstOwnershipUrlOrNull));
            Assert.Equal("2026-03-01T10:00:00-05:00", events.Current.GetProperty("startsAt").GetString());
            Assert.True(events.MoveNext());
            Assert.Equal("2026-03-08T10:00:00-04:00", events.Current.GetProperty("startsAt").GetString());
            Assert.True(events.MoveNext());
            Assert.Equal("2026-03-15T10:00:00-04:00", events.Current.GetProperty("startsAt").GetString());
            Assert.False(events.MoveNext());
        }
    }

    [Fact]
    public async Task ReplacementPassesExpectedCalendarIdForNativeRevalidationAsync()
    {
        RecordingAppleCalendarAutomationCommand command =
            new RecordingAppleCalendarAutomationCommand(
                """
                {
                  "status": "ok",
                  "calendarId": "managed:71f3be04-d4c6-41d4-a269-792321e71423:2026-2%ED%95%99%EA%B8%B0%20%EC%8B%9C%EA%B0%84%ED%91%9C",
                  "calendarName": "2026-2학기 시간표",
                  "createdEventCount": 3,
                  "deletedEventCount": 12
                }
                """);
        JxaAppleCalendarNativeBridge bridge = new JxaAppleCalendarNativeBridge(command);
        CalendarExportDocument document = createDocumentAcrossDstChange();
        AppleCalendarId existingCalendarId = new AppleCalendarId(
            "managed:71f3be04-d4c6-41d4-a269-792321e71423:2026-2%ED%95%99%EA%B8%B0%20%EC%8B%9C%EA%B0%84%ED%91%9C");

        AppleCalendarNativeExportResult result = await bridge.ApplyExportAsync(
            AppleCalendarExportMutation.ReplaceExisting(
                document,
                document.CalendarName,
                existingCalendarId),
            TestContext.Current.CancellationToken);

        Assert.Equal(existingCalendarId, result.CalendarId);
        Assert.Equal(12, result.DeletedEventCount);
        using (JsonDocument request = JsonDocument.Parse(Assert.Single(command.Invocations).RequestJson))
        {
            Assert.Equal(
                "replace",
                request.RootElement.GetProperty("mutationKind").GetString());
            Assert.Equal(
                existingCalendarId.Value,
                request.RootElement
                    .GetProperty("existingCalendarId")
                    .GetString());
            Assert.Equal(
                "2026-2학기 시간표".Normalize().ToUpperInvariant(),
                request.RootElement
                    .GetProperty("normalizedDestinationName")
                    .GetString());
            Assert.Equal(
                AppleCalendarOwnershipMarker.PREFIX,
                request.RootElement
                    .GetProperty("ownershipMarkerPrefix")
                    .GetString());
        }
    }

    [Fact]
    public void NativeReplacementContractRevalidatesEverySafetyPrecondition()
    {
        string script = AppleCalendarAutomationScript.SOURCE;

        Assert.Contains(
            "function findManagedCalendarById(calendars, id, markerPrefix)",
            script,
            StringComparison.Ordinal);
        Assert.Contains("if (match !== null)", script, StringComparison.Ordinal);
        Assert.Contains(
            "function replacementTargetIsValid(calendars, target, request)",
            script,
            StringComparison.Ordinal);
        Assert.Contains("matchingCalendars.length === 1", script, StringComparison.Ordinal);
        Assert.Contains(
            "request.ownershipMarkerPrefix) === expectedCalendarId",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "apple_calendar_replacement_target_changed",
            script,
            StringComparison.Ordinal);
        Assert.Contains("calendarApplication.calendars()", script, StringComparison.Ordinal);
        Assert.Contains("calendarId: request.existingCalendarId", script, StringComparison.Ordinal);
        Assert.Contains("calendarName: request.destinationName", script, StringComparison.Ordinal);
        Assert.Contains("calendarIsManaged(", script, StringComparison.Ordinal);
        Assert.Contains("request.ownershipMarkerPrefix", script, StringComparison.Ordinal);
        Assert.Contains(
            "const planIdPattern = /^[0-9a-f]{8}-",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "00000000-0000-0000-0000-000000000000",
            script,
            StringComparison.Ordinal);
        Assert.Contains("&& calendarIsWritable(target);", script, StringComparison.Ordinal);
        Assert.Contains("calendarApplication.Calendar({", script, StringComparison.Ordinal);
        Assert.Contains(").make();", script, StringComparison.Ordinal);
        Assert.Contains("calendar.events.push(event)", script, StringComparison.Ordinal);
        Assert.Contains(
            "url: eventData.ownershipUrl",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const previousEvents = findPreviousManagedEvents(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const replacementMappings = createReplacementEvents(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "restoreReplacementEventUrls(",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "url.indexOf(markerPrefix) !== 0",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "/^[0-9a-f]{64}$/.test(markerPayload)",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "const previousEvents = target.events();",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task NativeOperationMarkerRejectsCreatedAndReplacementTargetSwapsAsync()
    {
        if (OperatingSystem.IsMacOS() == false)
        {
            return;
        }

        const string HARNESS = """
            function replacementSuccessScenario() {
                const request = createMockRequest();
                const previousEvent = createMockEvent(
                    managedEventUrl("a"),
                    null);
                const target = createMockCalendar(
                    "target",
                    [previousEvent]);
                const createdEvents = [];
                const calendarApplication = {
                    calendars: function () {
                        return [target];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                const response = replaceCalendar(
                    calendarApplication,
                    request);
                return {
                    status: response.status,
                    createdEventCount:
                        response.createdEventCount,
                    deletedEventCount:
                        response.deletedEventCount,
                    previousEventDeleted:
                        previousEvent.deleted,
                    canaryDeleted:
                        createdEvents[0].deleted,
                    finalEventPreserved:
                        createdEvents[1].deleted === false,
                    finalEventUrl:
                        eventUrl(createdEvents[1]),
                };
            }

            function replacementSwapScenario() {
                const request = createMockRequest();
                const previousEvent = createMockEvent(
                    managedEventUrl("a"),
                    null);
                const original = createMockCalendar(
                    "original",
                    [previousEvent]);
                const replacement = createMockCalendar(
                    "replacement",
                    [createMockEvent(managedEventUrl("a"), null)]);
                const createdEvents = [];
                let snapshotCount = 0;
                const calendarApplication = {
                    calendars: function () {
                        snapshotCount += 1;
                        return snapshotCount === 1
                            ? [original]
                            : [replacement];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                let operationFailed = false;
                try {
                    replaceCalendar(
                        calendarApplication,
                        request);
                } catch (_) {
                    operationFailed = true;
                }

                return {
                    operationFailed: operationFailed,
                    canaryPreserved:
                        createdEvents[0].deleted === false,
                    previousEventPreserved:
                        previousEvent.deleted === false,
                };
            }

            function lateReplacementSwapScenario() {
                const request = createMockRequest();
                const previousEvent = createMockEvent(
                    managedEventUrl("a"),
                    null);
                const original = createMockCalendar(
                    "original",
                    [previousEvent]);
                const replacement = createMockCalendar(
                    "replacement",
                    [createMockEvent(managedEventUrl("a"), null)]);
                const createdEvents = [];
                let snapshotCount = 0;
                const calendarApplication = {
                    calendars: function () {
                        snapshotCount += 1;
                        return snapshotCount <= 2
                            ? [original]
                            : [replacement];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                let operationFailed = false;
                try {
                    replaceCalendar(
                        calendarApplication,
                        request);
                } catch (_) {
                    operationFailed = true;
                }

                return {
                    operationFailed: operationFailed,
                    canaryPreserved:
                        createdEvents[0].deleted === false,
                    createdEventPreserved:
                        createdEvents[1].deleted === false,
                    previousEventPreserved:
                        previousEvent.deleted === false,
                };
            }

            function postCommitSwapScenario() {
                const request = createMockRequest();
                const previousEvent = createMockEvent(
                    managedEventUrl("a"),
                    null);
                const original = createMockCalendar(
                    "original",
                    [previousEvent]);
                const replacement = createMockCalendar(
                    "replacement",
                    [createMockEvent(managedEventUrl("a"), null)]);
                const createdEvents = [];
                let snapshotCount = 0;
                const calendarApplication = {
                    calendars: function () {
                        snapshotCount += 1;
                        return snapshotCount <= 3
                            ? [original]
                            : [replacement];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                let operationFailed = false;
                try {
                    replaceCalendar(
                        calendarApplication,
                        request);
                } catch (_) {
                    operationFailed = true;
                }

                return {
                    operationFailed: operationFailed,
                    previousEventDeleted:
                        previousEvent.deleted,
                    createdEventPreserved:
                        createdEvents[1].deleted === false,
                    canaryPreserved:
                        createdEvents[0].deleted === false,
                    createdEventMarkerPreserved:
                        eventUrlIsManaged(
                            eventUrl(createdEvents[1]),
                            request.eventOwnershipMarkerPrefix),
                };
            }

            function uncertainCleanupScenario() {
                const request = createMockRequest();
                const previousEvent = createMockEvent(
                    managedEventUrl("a"),
                    null);
                const original = createMockCalendar(
                    "original",
                    [previousEvent]);
                const replacement = createMockCalendar(
                    "replacement",
                    [createMockEvent(managedEventUrl("a"), null)]);
                const createdEvents = [];
                let snapshotCount = 0;
                const calendarApplication = {
                    calendars: function () {
                        snapshotCount += 1;
                        return snapshotCount === 1
                            ? [original]
                            : [replacement];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            function (_) {
                                throw new Error(
                                    "synthetic_cleanup_failure");
                            });
                        createdEvents.push(event);
                        return event;
                    },
                };

                let operationFailed = false;
                try {
                    replaceCalendar(
                        calendarApplication,
                        request);
                } catch (_) {
                    operationFailed = true;
                }

                return {
                    operationFailed: operationFailed,
                    canaryPreserved:
                        createdEvents[0].deleted === false,
                    previousEventPreserved:
                        previousEvent.deleted === false,
                };
            }

            function creationSwapScenario() {
                const request = createMockRequest();
                const original = createMockCalendar("created", []);
                const replacement = createMockCalendar("replacement", []);
                const createdEvents = [];
                let snapshotCount = 0;
                const calendarApplication = {
                    calendars: function () {
                        snapshotCount += 1;
                        if (snapshotCount === 1) {
                            return [];
                        }

                        return snapshotCount === 2
                            ? [original]
                            : [replacement];
                    },
                    Calendar: function (_) {
                        return {
                            make: function () {
                                return original;
                            },
                        };
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                let operationFailed = false;
                try {
                    createCalendar(
                        calendarApplication,
                        request);
                } catch (_) {
                    operationFailed = true;
                }

                return {
                    operationFailed: operationFailed,
                    canaryPreserved:
                        createdEvents[0].deleted === false,
                    createdEventPreserved:
                        createdEvents[1].deleted === false,
                    createdCalendarPreserved:
                        original.deleted === false,
                };
            }

            function run(_) {
                return JSON.stringify({
                    success: replacementSuccessScenario(),
                    replacement: replacementSwapScenario(),
                    lateReplacement:
                        lateReplacementSwapScenario(),
                    postCommit: postCommitSwapScenario(),
                    uncertainCleanup:
                        uncertainCleanupScenario(),
                    creation: creationSwapScenario(),
                });
            }
            """;

        using (JsonDocument result = await executeAutomationSourceHarnessAsync(HARNESS))
        {
            JsonElement success =
                result.RootElement.GetProperty("success");
            Assert.Equal(
                "ok",
                success.GetProperty("status").GetString());
            Assert.Equal(
                1,
                success
                    .GetProperty("createdEventCount")
                    .GetInt32());
            Assert.Equal(
                1,
                success
                    .GetProperty("deletedEventCount")
                    .GetInt32());
            Assert.True(
                success
                    .GetProperty("previousEventDeleted")
                    .GetBoolean());
            Assert.True(
                success
                    .GetProperty("canaryDeleted")
                    .GetBoolean());
            Assert.True(
                success
                    .GetProperty("finalEventPreserved")
                    .GetBoolean());
            Assert.Equal(
                "timetable-generator://managed-event/v1/"
                    + new string('c', 64),
                success
                    .GetProperty("finalEventUrl")
                    .GetString());

            JsonElement replacement =
                result.RootElement.GetProperty("replacement");
            Assert.True(
                replacement
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                replacement
                    .GetProperty("canaryPreserved")
                    .GetBoolean());
            Assert.True(
                replacement
                    .GetProperty("previousEventPreserved")
                    .GetBoolean());

            JsonElement lateReplacement =
                result.RootElement.GetProperty("lateReplacement");
            Assert.True(
                lateReplacement
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                lateReplacement
                    .GetProperty("canaryPreserved")
                    .GetBoolean());
            Assert.True(
                lateReplacement
                    .GetProperty("createdEventPreserved")
                    .GetBoolean());
            Assert.True(
                lateReplacement
                    .GetProperty("previousEventPreserved")
                    .GetBoolean());

            JsonElement postCommit =
                result.RootElement.GetProperty("postCommit");
            Assert.True(
                postCommit
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                postCommit
                    .GetProperty("previousEventDeleted")
                    .GetBoolean());
            Assert.True(
                postCommit
                    .GetProperty("createdEventPreserved")
                    .GetBoolean());
            Assert.True(
                postCommit
                    .GetProperty("canaryPreserved")
                    .GetBoolean());
            Assert.True(
                postCommit
                    .GetProperty("createdEventMarkerPreserved")
                    .GetBoolean());

            JsonElement uncertainCleanup =
                result.RootElement.GetProperty("uncertainCleanup");
            Assert.True(
                uncertainCleanup
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                uncertainCleanup
                    .GetProperty("canaryPreserved")
                    .GetBoolean());
            Assert.True(
                uncertainCleanup
                    .GetProperty("previousEventPreserved")
                    .GetBoolean());

            JsonElement creation =
                result.RootElement.GetProperty("creation");
            Assert.True(
                creation
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                creation
                    .GetProperty("canaryPreserved")
                    .GetBoolean());
            Assert.True(
                creation
                    .GetProperty("createdEventPreserved")
                    .GetBoolean());
            Assert.True(
                creation
                    .GetProperty("createdCalendarPreserved")
                    .GetBoolean());
        }
    }

    [Fact]
    public async Task NativeCanaryProtectsEmptyCreateAndReplacementAsync()
    {
        if (OperatingSystem.IsMacOS() == false)
        {
            return;
        }

        const string HARNESS = """
            function emptyReplacementSuccessScenario() {
                const request = createMockRequest();
                request.events = [];
                const previousEvent = createMockEvent(
                    managedEventUrl("a"),
                    null);
                const target = createMockCalendar(
                    "target",
                    [previousEvent]);
                const createdEvents = [];
                const calendarApplication = {
                    calendars: function () {
                        return [target];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                const response = replaceCalendar(
                    calendarApplication,
                    request);
                return {
                    status: response.status,
                    createdEventCount:
                        response.createdEventCount,
                    deletedEventCount:
                        response.deletedEventCount,
                    previousEventDeleted:
                        previousEvent.deleted,
                    canaryDeleted:
                        createdEvents[0].deleted,
                };
            }

            function emptyReplacementPostCommitSwapScenario() {
                const request = createMockRequest();
                request.events = [];
                const previousEvent = createMockEvent(
                    managedEventUrl("a"),
                    null);
                const original = createMockCalendar(
                    "original",
                    [previousEvent]);
                const replacement = createMockCalendar(
                    "replacement",
                    []);
                const createdEvents = [];
                let snapshotCount = 0;
                const calendarApplication = {
                    calendars: function () {
                        snapshotCount += 1;
                        return snapshotCount <= 3
                            ? [original]
                            : [replacement];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                let operationFailed = false;
                try {
                    replaceCalendar(
                        calendarApplication,
                        request);
                } catch (_) {
                    operationFailed = true;
                }

                return {
                    operationFailed: operationFailed,
                    previousEventDeleted:
                        previousEvent.deleted,
                    canaryPreserved:
                        createdEvents[0].deleted === false,
                };
            }

            function emptyCreationSuccessScenario() {
                const request = createMockRequest();
                request.events = [];
                const target = createMockCalendar(
                    "created",
                    []);
                const createdEvents = [];
                let snapshotCount = 0;
                const calendarApplication = {
                    calendars: function () {
                        snapshotCount += 1;
                        return snapshotCount === 1
                            ? []
                            : [target];
                    },
                    Calendar: function (_) {
                        return {
                            make: function () {
                                return target;
                            },
                        };
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                const response = createCalendar(
                    calendarApplication,
                    request);
                return {
                    status: response.status,
                    createdEventCount:
                        response.createdEventCount,
                    canaryDeleted:
                        createdEvents[0].deleted,
                    calendarPreserved:
                        target.deleted === false,
                };
            }

            function duplicateSnapshotScenario() {
                const request = createMockRequest();
                const first = createMockCalendar(
                    "first",
                    []);
                const second = createMockCalendar(
                    "second",
                    []);
                const response = listCalendars(
                    {
                        calendars: function () {
                            return [first, second];
                        },
                    },
                    request);
                return {
                    status: response.status,
                    firstId: response.calendars[0].id,
                    secondId: response.calendars[1].id,
                };
            }

            function invalidInitialCreationScenario() {
                const request = createMockRequest();
                const target = createMockCalendar(
                    "invalid",
                    []);
                target.description = function () {
                    return "";
                };
                const calendarApplication = {
                    calendars: function () {
                        return [];
                    },
                    Calendar: function (_) {
                        return {
                            make: function () {
                                return target;
                            },
                        };
                    },
                };

                let operationFailed = false;
                try {
                    createCalendar(
                        calendarApplication,
                        request);
                } catch (_) {
                    operationFailed = true;
                }

                return {
                    operationFailed: operationFailed,
                    invalidCalendarPreserved:
                        target.deleted === false,
                };
            }

            function run(_) {
                return JSON.stringify({
                    replacement:
                        emptyReplacementSuccessScenario(),
                    postCommit:
                        emptyReplacementPostCommitSwapScenario(),
                    creation:
                        emptyCreationSuccessScenario(),
                    duplicate:
                        duplicateSnapshotScenario(),
                    invalidCreation:
                        invalidInitialCreationScenario(),
                });
            }
            """;

        using (JsonDocument result =
            await executeAutomationSourceHarnessAsync(HARNESS))
        {
            JsonElement replacement =
                result.RootElement.GetProperty("replacement");
            Assert.Equal(
                "ok",
                replacement.GetProperty("status").GetString());
            Assert.Equal(
                0,
                replacement
                    .GetProperty("createdEventCount")
                    .GetInt32());
            Assert.Equal(
                1,
                replacement
                    .GetProperty("deletedEventCount")
                    .GetInt32());
            Assert.True(
                replacement
                    .GetProperty("previousEventDeleted")
                    .GetBoolean());
            Assert.True(
                replacement
                    .GetProperty("canaryDeleted")
                    .GetBoolean());

            JsonElement postCommit =
                result.RootElement.GetProperty("postCommit");
            Assert.True(
                postCommit
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                postCommit
                    .GetProperty("previousEventDeleted")
                    .GetBoolean());
            Assert.True(
                postCommit
                    .GetProperty("canaryPreserved")
                    .GetBoolean());

            JsonElement creation =
                result.RootElement.GetProperty("creation");
            Assert.Equal(
                "ok",
                creation.GetProperty("status").GetString());
            Assert.Equal(
                0,
                creation
                    .GetProperty("createdEventCount")
                    .GetInt32());
            Assert.True(
                creation
                    .GetProperty("canaryDeleted")
                    .GetBoolean());
            Assert.True(
                creation
                    .GetProperty("calendarPreserved")
                    .GetBoolean());

            JsonElement duplicate =
                result.RootElement.GetProperty("duplicate");
            Assert.Equal(
                "ok",
                duplicate.GetProperty("status").GetString());
            string? firstIdOrNull =
                duplicate.GetProperty("firstId").GetString();
            string? secondIdOrNull =
                duplicate.GetProperty("secondId").GetString();
            Assert.NotNull(firstIdOrNull);
            Assert.NotNull(secondIdOrNull);
            Assert.StartsWith(
                "ambiguous:0:managed:",
                firstIdOrNull,
                StringComparison.Ordinal);
            Assert.StartsWith(
                "ambiguous:1:managed:",
                secondIdOrNull,
                StringComparison.Ordinal);
            Assert.NotEqual(firstIdOrNull, secondIdOrNull);

            JsonElement invalidCreation =
                result.RootElement.GetProperty("invalidCreation");
            Assert.True(
                invalidCreation
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                invalidCreation
                    .GetProperty("invalidCalendarPreserved")
                    .GetBoolean());
        }
    }

    [Fact]
    public async Task NativeUnreadableEventUrlFailsClosedAsync()
    {
        if (OperatingSystem.IsMacOS() == false)
        {
            return;
        }

        const string HARNESS = """
            function run(_) {
                const request = createMockRequest();
                const unreadableEvent = {
                    deleted: false,
                    url: function () {
                        throw new Error(
                            "synthetic_url_read_failure");
                    },
                    delete: function () {
                        unreadableEvent.deleted = true;
                    },
                };
                const target = createMockCalendar(
                    "target",
                    [unreadableEvent]);
                const createdEvents = [];
                const calendarApplication = {
                    calendars: function () {
                        return [target];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                let operationFailed = false;
                try {
                    replaceCalendar(
                        calendarApplication,
                        request);
                } catch (_) {
                    operationFailed = true;
                }

                return JSON.stringify({
                    operationFailed: operationFailed,
                    unreadableEventPreserved:
                        unreadableEvent.deleted === false,
                    canaryPreserved:
                        createdEvents[0].deleted === false,
                    replacementNotCreated:
                        createdEvents.length === 1,
                });
            }
            """;

        using (JsonDocument result =
            await executeAutomationSourceHarnessAsync(HARNESS))
        {
            Assert.True(
                result.RootElement
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("unreadableEventPreserved")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("canaryPreserved")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("replacementNotCreated")
                    .GetBoolean());
        }
    }

    [Fact]
    public async Task NativeSilentOldEventDeletionCannotReportSuccessAsync()
    {
        if (OperatingSystem.IsMacOS() == false)
        {
            return;
        }

        const string HARNESS = """
            function run(_) {
                const request = createMockRequest();
                const silentDeletion = createMockEvent(
                    managedEventUrl("a"),
                    function (_) {
                        // Synthetic provider acknowledges without deleting.
                    });
                const target = createMockCalendar(
                    "target",
                    [silentDeletion]);
                const createdEvents = [];
                const calendarApplication = {
                    calendars: function () {
                        return [target];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                let operationFailed = false;
                try {
                    replaceCalendar(
                        calendarApplication,
                        request);
                } catch (_) {
                    operationFailed = true;
                }

                return JSON.stringify({
                    operationFailed: operationFailed,
                    previousEventPreserved:
                        silentDeletion.deleted === false,
                    canaryPreserved:
                        createdEvents[0].deleted === false,
                    newEventPreserved:
                        createdEvents[1].deleted === false,
                    newEventMarkerManaged:
                        eventUrlIsManaged(
                            eventUrl(createdEvents[1]),
                            request.eventOwnershipMarkerPrefix),
                });
            }
            """;

        using (JsonDocument result =
            await executeAutomationSourceHarnessAsync(HARNESS))
        {
            Assert.True(
                result.RootElement
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("previousEventPreserved")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("canaryPreserved")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("newEventPreserved")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("newEventMarkerManaged")
                    .GetBoolean());
        }
    }

    [Fact]
    public async Task NativePartialOldEventDeletionPreservesNewManagedEventsAsync()
    {
        if (OperatingSystem.IsMacOS() == false)
        {
            return;
        }

        const string HARNESS = """
            function run(_) {
                const request = createMockRequest();
                const deletionFailure = createMockEvent(
                    managedEventUrl("a"),
                    function (_) {
                        throw new Error("synthetic_delete_failure");
                    });
                const deletedBeforeFailure = createMockEvent(
                    managedEventUrl("b"),
                    null);
                const target = createMockCalendar(
                    "target",
                    [deletionFailure, deletedBeforeFailure]);
                const createdEvents = [];
                const calendarApplication = {
                    calendars: function () {
                        return [target];
                    },
                    Event: function (eventData) {
                        const event = createMockEvent(
                            eventData.url,
                            null);
                        createdEvents.push(event);
                        return event;
                    },
                };

                let operationFailed = false;
                try {
                    replaceCalendar(calendarApplication, request);
                } catch (_) {
                    operationFailed = true;
                }

                return JSON.stringify({
                    operationFailed: operationFailed,
                    oneOldEventDeleted:
                        deletedBeforeFailure.deleted,
                    remainingOldEventPreserved:
                        deletionFailure.deleted === false,
                    canaryPreserved:
                        createdEvents[0].deleted === false,
                    newEventPreserved:
                        createdEvents[1].deleted === false,
                    newEventMarkerPreserved:
                        eventUrlIsManaged(
                            eventUrl(createdEvents[1]),
                            request.eventOwnershipMarkerPrefix),
                });
            }
            """;

        using (JsonDocument result = await executeAutomationSourceHarnessAsync(HARNESS))
        {
            Assert.True(
                result.RootElement
                    .GetProperty("operationFailed")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("oneOldEventDeleted")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("remainingOldEventPreserved")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("canaryPreserved")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("newEventPreserved")
                    .GetBoolean());
            Assert.True(
                result.RootElement
                    .GetProperty("newEventMarkerPreserved")
                    .GetBoolean());
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("timetable-generator://managed-event/v1/")]
    [InlineData("timetable-generator://managed-event/v1/not-a-hash")]
    [InlineData("timetable-generator://managed-event/v1/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void EventOwnershipRejectsMissingOrNonCanonicalMarkers(
        string? markerOrNull)
    {
        Assert.False(
            AppleCalendarEventOwnershipMarker.IsApplicationManaged(
                markerOrNull));
    }

    [Fact]
    public void EventOwnershipCreatesDeterministicCanonicalMarkers()
    {
        string first = AppleCalendarEventOwnershipMarker.Create(
            "personal:lab:2026-03-01");
        string second = AppleCalendarEventOwnershipMarker.Create(
            "personal:lab:2026-03-01");

        Assert.Equal(first, second);
        Assert.StartsWith(
            AppleCalendarEventOwnershipMarker.PREFIX,
            first,
            StringComparison.Ordinal);
        Assert.True(
            AppleCalendarEventOwnershipMarker.IsApplicationManaged(first));
    }

    [Theory]
    [InlineData("access_denied", 1)]
    [InlineData("calendar_changed", 2)]
    [InlineData("operation_failed", 4)]
    public async Task NativeFailuresAreClassifiedAsync(
        string responseStatus,
        int expectedFailureKindValue)
    {
        RecordingAppleCalendarAutomationCommand command =
            new RecordingAppleCalendarAutomationCommand(
                "{\"status\":\"" + responseStatus + "\"}");
        JxaAppleCalendarNativeBridge bridge = new JxaAppleCalendarNativeBridge(command);

        AppleCalendarNativeBridgeException exception =
            await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
                () => bridge.GetCalendarsAsync(
                    TestContext.Current.CancellationToken));

        Assert.Equal((EAppleCalendarNativeFailureKind)expectedFailureKindValue, exception.FailureKind);
    }

    [Fact]
    public async Task UnavailableCommandIsRejectedBeforeExecutionAsync()
    {
        RecordingAppleCalendarAutomationCommand command =
            new RecordingAppleCalendarAutomationCommand(
                "{\"status\":\"ok\",\"calendars\":[]}")
            {
                IsAvailable = false,
            };
        JxaAppleCalendarNativeBridge bridge = new JxaAppleCalendarNativeBridge(command);

        AppleCalendarNativeBridgeException exception =
            await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
                () => bridge.GetCalendarsAsync(
                    TestContext.Current.CancellationToken));

        Assert.Equal(EAppleCalendarNativeFailureKind.Unavailable, exception.FailureKind);
        Assert.Empty(command.Invocations);
    }

    [Fact]
    public void ProcessArgumentsContainOnlyStaticScriptAndRequestPath()
    {
        const string REQUEST_PATH = "/tmp/calendar-request-012345.json";
        const string USER_CONTENT = "민감한 일정 이름";

        ProcessStartInfo startInfo =
            ProcessAppleCalendarAutomationCommand.createStartInfo(
                EAppleCalendarAutomationOperation.ApplyExport,
                REQUEST_PATH);

        Assert.False(startInfo.UseShellExecute);
        Assert.Equal("/usr/bin/osascript", startInfo.FileName);
        Assert.Contains(REQUEST_PATH, startInfo.ArgumentList);
        Assert.DoesNotContain(
            startInfo.ArgumentList,
            argument => argument.Contains(
                USER_CONTENT,
                StringComparison.Ordinal));
        Assert.Equal("apply", startInfo.ArgumentList[^2]);
        Assert.Equal(REQUEST_PATH, startInfo.ArgumentList[^1]);
    }

    private static async Task<JsonDocument>
        executeAutomationSourceHarnessAsync(string harness)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = "/usr/bin/osascript";
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add("JavaScript");
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(
            AppleCalendarAutomationScript.SOURCE
                + Environment.NewLine
                + AUTOMATION_FAULT_HARNESS_SUPPORT
                + Environment.NewLine
                + harness);

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;
            Assert.True(process.Start());
            Task<string> standardOutputTask =
                process.StandardOutput.ReadToEndAsync(
                    TestContext.Current.CancellationToken);
            Task<string> standardErrorTask =
                process.StandardError.ReadToEndAsync(
                    TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(
                TestContext.Current.CancellationToken);
            string standardOutput = await standardOutputTask;
            await standardErrorTask;
            Assert.True(
                process.ExitCode == 0,
                "Synthetic Apple Calendar JXA harness failed.");
            Assert.False(string.IsNullOrWhiteSpace(standardOutput));
            return JsonDocument.Parse(standardOutput.Trim());
        }
    }

    private const string AUTOMATION_FAULT_HARNESS_SUPPORT = """
        const testCalendarMarkerPrefix =
            "timetable-generator://managed-calendar/v1/";
        const testEventMarkerPrefix =
            "timetable-generator://managed-event/v1/";
        const testPlanId =
            "71f3be04-d4c6-41d4-a269-792321e71423";
        const testCalendarId =
            "managed:" + testPlanId + ":QA";

        function managedEventUrl(character) {
            return testEventMarkerPrefix
                + character.repeat(64);
        }

        function createMockEvent(initialUrl, deleteAction) {
            let currentUrl = initialUrl;
            const event = {
                deleted: false,
                url: function () {
                    return event.deleted ? "" : currentUrl;
                },
                delete: function () {
                    if (deleteAction === null) {
                        event.deleted = true;
                        return;
                    }

                    deleteAction(event);
                },
            };

            return new Proxy(event, {
                set: function (target, property, value) {
                    if (property === "url") {
                        currentUrl = String(value);
                        return true;
                    }

                    target[property] = value;
                    return true;
                },
            });
        }

        function createMockCalendar(label, events) {
            function eventCollection() {
                return events;
            }

            eventCollection.push = function (event) {
                events.push(event);
            };
            const calendar = {
                label: label,
                deleted: false,
                name: function () {
                    return "QA";
                },
                description: function () {
                    return testCalendarMarkerPrefix
                        + testPlanId;
                },
                writable: function () {
                    return true;
                },
                events: eventCollection,
                delete: function () {
                    calendar.deleted = true;
                },
            };
            return calendar;
        }

        function createMockRequest() {
            return {
                destinationName: "QA",
                normalizedDestinationName: "QA",
                ownershipMarkerPrefix:
                    testCalendarMarkerPrefix,
                ownershipDescription:
                    testCalendarMarkerPrefix + testPlanId,
                eventOwnershipMarkerPrefix:
                    testEventMarkerPrefix,
                existingCalendarId: testCalendarId,
                events: [
                    {
                        summary: "Synthetic",
                        location: "Synthetic",
                        description: "Synthetic",
                        startsAt: "2026-01-01T00:00:00Z",
                        endsAt: "2026-01-01T01:00:00Z",
                        ownershipUrl: managedEventUrl("c"),
                    },
                ],
            };
        }
        """;

    private static CalendarExportDocument createDocumentAcrossDstChange()
    {
        AcademicTermCalendarMetadata academicCalendar =
            new AcademicTermCalendarMetadata(
                AcademicTerm.Parse("2026-2"),
                new AcademicTermDateRange(
                    new DateOnly(2026, 3, 1),
                    new DateOnly(2026, 3, 15)),
                new CalendarTimeZoneId("America/New_York"));
        RecurringCalendarEvent calendarEvent = new RecurringCalendarEvent(
            new CalendarEventUid("personal:lab"),
            new CalendarEventContent(
                "랩 미팅",
                "Grace Hopper Lab",
                "고정 일정"),
            new DailyTimeRange(
                new ScheduleTime(10, 0),
                new ScheduleTime(11, 15)),
            new EDay[] { EDay.Sunday });
        return new CalendarExportDocument(
            new PlanId(
                Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423")),
            new PlanName("2026-2학기 시간표"),
            academicCalendar,
            new RecurringCalendarEvent[] { calendarEvent });
    }

    private sealed record AppleCalendarAutomationInvocation(
        EAppleCalendarAutomationOperation Operation,
        string RequestJson);

    private sealed class RecordingAppleCalendarAutomationCommand
        : IAppleCalendarAutomationCommand
    {
        private readonly string mResponseJson;
        private readonly List<AppleCalendarAutomationInvocation> mInvocations = new List<AppleCalendarAutomationInvocation>();

        public bool IsAvailable { get; set; } = true;

        public IReadOnlyList<AppleCalendarAutomationInvocation> Invocations
        {
            get
            {
                return mInvocations;
            }
        }

        public RecordingAppleCalendarAutomationCommand(string responseJson)
        {
            mResponseJson = responseJson;
        }

        public Task<string> ExecuteAsync(
            EAppleCalendarAutomationOperation operation,
            string requestJson,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mInvocations.Add(new AppleCalendarAutomationInvocation(operation, requestJson));
            return Task.FromResult(mResponseJson);
        }
    }
}
