using System;
using System.Net.Http;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Planning;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    [Fact]
    public async Task ApplicationManagedNonPrimaryCalendarCanBeReplacedAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string marker = GoogleCalendarApiClient.createPlanMarker(new PlanId(Guid.Parse("5c113dab-0fe8-4c86-a69f-ef657e21314b")));
        string listJson = createCalendarListJson(createCalendarJson("managed-calendar", plan.CalendarName.Value, false, marker));
        GoogleCalendarEventId staleEventId = GoogleCalendarEventId.Create(plan.PlanId, new GoogleCalendarSourceEventId("stale-event"));
        string eventListJson = "{\"items\":[" + "{\"id\":\"" + staleEventId.Value + "\"," + "\"extendedProperties\":{\"private\":{" + "\"timetableGeneratorManaged\":\"true\"," + "\"timetableGeneratorPlanId\":\"" + plan.PlanId.Value.ToString("N") + "\"}}}," + "{\"id\":\"manual-event\"}]}";
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(listJson, listJson)
        {
            EventListJson = eventListJson,
        };
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.Equal("managed-calendar", result.CalendarIdOrNull?.Value);
        Assert.True(resolver.ConflictOrNull?.CanReplace);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                && request.Path.EndsWith("/calendars/managed-calendar", StringComparison.Ordinal)
                && hasCalendarDescription(request, "한동대학교 2026-2 시간표입니다."));
        Assert.Single(handler.Requests, request => request.Method == HttpMethod.Delete);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith(staleEventId.Value, StringComparison.Ordinal));
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith("manual-event", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MultipleMatchingManagedCalendarsCannotBeReplacedAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string marker = GoogleCalendarApiClient.createPlanMarker(plan.PlanId);
        string listJson = createCalendarListJson(createCalendarJson("managed-calendar-one", plan.CalendarName.Value, false, marker), createCalendarJson("managed-calendar-two", plan.CalendarName.Value, false, marker));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.Equal("google_calendar_local_state_failed", result.DiagnosticCodeOrNull);
        Assert.False(resolver.ConflictOrNull?.CanReplace);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task FriendlyDescriptionCalendarUsesManagedEventOwnershipForReplacementAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        GoogleCalendarEventId existingEventId = GoogleCalendarEventId.Create(plan.PlanId, plan.Events[0].SourceId);
        string listJson = createCalendarListJson(createCalendarJson("managed-calendar", plan.CalendarName.Value, false, plan.CalendarDescription.Value));
        string eventListJson = "{\"items\":[" + "{\"id\":\"" + existingEventId.Value + "\",\"extendedProperties\":{\"private\":{" + "\"timetableGeneratorManaged\":\"true\"," + "\"timetableGeneratorPlanId\":\"" + plan.PlanId.Value.ToString("N") + "\"}}}]}";
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(listJson, listJson)
        {
            EventListJson = eventListJson,
        };
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.True(resolver.ConflictOrNull?.CanReplace);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                && request.Path.EndsWith("/calendars/managed-calendar", StringComparison.Ordinal)
                && hasCalendarDescription(request, "한동대학교 2026-2 시간표입니다."));
    }

    [Fact]
    public async Task ReplacingFriendlyCalendarFromAnotherPlanRemovesItsManagedEventsAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        PlanId replacedPlanId = new PlanId(Guid.Parse("94a5bfba-29bd-4d88-98f4-f457a6a2eb3f"));
        GoogleCalendarEventId replacedEventId = GoogleCalendarEventId.Create(replacedPlanId, new GoogleCalendarSourceEventId("replaced-course"));
        string listJson = createCalendarListJson(createCalendarJson("managed-calendar", plan.CalendarName.Value, false, plan.CalendarDescription.Value));
        string eventListJson = "{\"items\":[" + "{\"id\":\"" + replacedEventId.Value + "\",\"extendedProperties\":{\"private\":{" + "\"timetableGeneratorManaged\":\"true\"," + "\"timetableGeneratorPlanId\":\"" + replacedPlanId.Value.ToString("N") + "\"}}}]}";
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(listJson, listJson)
        {
            EventListJson = eventListJson,
        };
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith(replacedEventId.Value, StringComparison.Ordinal));
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post
                && request.Path.Contains("/events", StringComparison.Ordinal));
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                && request.Path.EndsWith("/calendars/managed-calendar", StringComparison.Ordinal)
                && hasCalendarDescription(request, GoogleCalendarApiClient.createPlanMarker(replacedPlanId)));
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                && request.Path.EndsWith("/calendars/managed-calendar", StringComparison.Ordinal)
                && hasCalendarDescription(request, "한동대학교 2026-2 시간표입니다."));
    }

    [Fact]
    public async Task FriendlyDescriptionWithoutManagedEventsCannotBeReplacedAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string listJson = createCalendarListJson(createCalendarJson("user-calendar", plan.CalendarName.Value, false, plan.CalendarDescription.Value));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Cancelled, result.Status);
        Assert.False(resolver.ConflictOrNull?.CanReplace);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Theory]
    [InlineData("owner", true)]
    [InlineData("writer", true)]
    [InlineData("writerWithoutPrivateAccess", true)]
    [InlineData("reader", false)]
    [InlineData("freeBusyReader", false)]
    [InlineData("futureRole", false)]
    [InlineData(null, false)]
    public async Task ReplacementRequiresAWriteCapableAccessRoleAsync(string? accessRoleOrNull, bool expectedCanReplace)
    {
        GoogleCalendarExportPlan plan = createPlan();
        string listJson = createCalendarListJson(
            createCalendarJson(
                "managed-calendar",
                plan.CalendarName.Value,
                false,
                GoogleCalendarApiClient.createPlanMarker(plan.PlanId),
                accessRoleOrNull));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Cancelled, result.Status);
        Assert.Equal(expectedCanReplace, resolver.ConflictOrNull?.CanReplace);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task PrimaryOrUnmanagedCalendarCannotBeReplacedAsync(bool isPrimary, bool hasApplicationMarker)
    {
        GoogleCalendarExportPlan plan = createPlan();
        string? markerOrNull = hasApplicationMarker ? GoogleCalendarApiClient.createPlanMarker(plan.PlanId) : null;
        string listJson = createCalendarListJson(createCalendarJson("protected-calendar", plan.CalendarName.Value, isPrimary, markerOrNull));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.False(resolver.ConflictOrNull?.CanReplace);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }
}
