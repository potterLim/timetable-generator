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
                      "id": "managed",
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
        Assert.Equal("managed", calendars[0].CalendarId.Value);
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
    public async Task CreateExpandsWeeklyEventsWithOccurrenceSpecificOffsetsAsync()
    {
        RecordingAppleCalendarAutomationCommand command =
            new RecordingAppleCalendarAutomationCommand(
                """
                {
                  "status": "ok",
                  "calendarId": "created",
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

        Assert.Equal("created", result.CalendarId.Value);
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
                  "calendarId": "existing",
                  "calendarName": "2026-2학기 시간표",
                  "createdEventCount": 3,
                  "deletedEventCount": 12
                }
                """);
        JxaAppleCalendarNativeBridge bridge = new JxaAppleCalendarNativeBridge(command);
        CalendarExportDocument document = createDocumentAcrossDstChange();

        AppleCalendarNativeExportResult result = await bridge.ApplyExportAsync(
            AppleCalendarExportMutation.ReplaceExisting(
                document,
                document.CalendarName,
                new AppleCalendarId("existing")),
            TestContext.Current.CancellationToken);

        Assert.Equal(12, result.DeletedEventCount);
        using (JsonDocument request = JsonDocument.Parse(Assert.Single(command.Invocations).RequestJson))
        {
            Assert.Equal("replace", request.RootElement.GetProperty("mutationKind").GetString());
            Assert.Equal("existing", request.RootElement.GetProperty("existingCalendarId").GetString());
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

        Assert.Contains("matchingCalendars.length !== 1", script, StringComparison.Ordinal);
        Assert.Contains(
            "calendarId(matchingCalendars[0]) !== request.existingCalendarId",
            script,
            StringComparison.Ordinal);
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
        Assert.Contains("calendarIsWritable(target) === false", script, StringComparison.Ordinal);
        Assert.Contains("calendarApplication.Calendar({", script, StringComparison.Ordinal);
        Assert.Contains(").make();", script, StringComparison.Ordinal);
        Assert.Contains("calendar.events.push(event)", script, StringComparison.Ordinal);
        Assert.Contains(
            "url: eventData.ownershipUrl",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "const previousEvents = findManagedEvents(",
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
