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
    public async Task CancellingNameConflictDoesNotMutateGoogleCalendarAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string listJson = createCalendarListJson(createCalendarJson("existing", plan.CalendarName.Value, false, GoogleCalendarApiClient.createPlanMarker(plan.PlanId)));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Cancelled, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesOwnershipBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string firstListJson = createCalendarListJson(createCalendarJson("managed-calendar", plan.CalendarName.Value, false, GoogleCalendarApiClient.createPlanMarker(plan.PlanId)));
        string secondListJson = createCalendarListJson(createCalendarJson("managed-calendar", plan.CalendarName.Value, false, null));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesTheConfirmedCalendarIdBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string marker = GoogleCalendarApiClient.createPlanMarker(plan.PlanId);
        string firstListJson = createCalendarListJson(createCalendarJson("confirmed-calendar", plan.CalendarName.Value, false, marker));
        string secondListJson = createCalendarListJson(createCalendarJson("different-calendar", plan.CalendarName.Value, false, marker));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.True(resolver.ConflictOrNull?.CanReplace);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesTheConfirmedCalendarNameBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string marker = GoogleCalendarApiClient.createPlanMarker(plan.PlanId);
        string firstListJson = createCalendarListJson(createCalendarJson("confirmed-calendar", plan.CalendarName.Value, false, marker));
        string secondListJson = createCalendarListJson(createCalendarJson("confirmed-calendar", "이름이 바뀐 시간표", false, marker));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesTheManagementMarkerBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string firstListJson = createCalendarListJson(createCalendarJson("confirmed-calendar", plan.CalendarName.Value, false, GoogleCalendarApiClient.createPlanMarker(plan.PlanId)));
        string secondListJson = createCalendarListJson(createCalendarJson("confirmed-calendar", plan.CalendarName.Value, false, GoogleCalendarApiClient.createPlanMarker(PlanId.CreateNew())));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReplacementRevalidatesWriteAccessBeforeMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string marker = GoogleCalendarApiClient.createPlanMarker(plan.PlanId);
        string firstListJson = createCalendarListJson(
            createCalendarJson(
                "confirmed-calendar",
                plan.CalendarName.Value,
                false,
                marker,
                "owner"));
        string secondListJson = createCalendarListJson(
            createCalendarJson(
                "confirmed-calendar",
                plan.CalendarName.Value,
                false,
                marker,
                "reader"));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(firstListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.ReplaceExisting);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }
}
