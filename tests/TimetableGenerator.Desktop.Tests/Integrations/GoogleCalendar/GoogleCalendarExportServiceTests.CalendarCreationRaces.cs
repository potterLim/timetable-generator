using System.Net.Http;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    [Fact]
    public async Task OccupiedSuggestedNameIsConfirmedAgainBeforeCreationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string requestedCalendar = createCalendarJson("requested-calendar", plan.CalendarName.Value, false, null);
        string firstListJson = createCalendarListJson(requestedCalendar);
        string secondListJson = createCalendarListJson(requestedCalendar, createCalendarJson("first-copy", plan.CalendarName.Value + " (2)", false, null));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(firstListJson, secondListJson, secondListJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (3)", result.CalendarNameOrNull?.Value);
        Assert.Equal(2, resolver.CallCount);
        Assert.Equal(2, resolver.Conflicts.Count);
        Assert.Equal("2026-2학기 시간표 (2)", resolver.Conflicts[0].NextAvailableName.Value);
        Assert.Equal("2026-2학기 시간표 (3)", resolver.Conflicts[1].NextAvailableName.Value);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post
                && hasCalendarSummary(request, "2026-2학기 시간표 (3)"));
    }

    [Fact]
    public async Task ChangedConflictDoesNotSilentlyRestoreTheRequestedNameAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string requestedCalendar = createCalendarJson("requested-calendar", plan.CalendarName.Value, false, null);
        string occupiedCopy = createCalendarJson("first-copy", plan.CalendarName.Value + " (2)", false, null);
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(createCalendarListJson(requestedCalendar), createCalendarListJson(occupiedCopy));
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.Equal(1, resolver.CallCount);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task ReconfirmedSuggestedNameCanBeCancelledWithoutMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string requestedCalendar = createCalendarJson("requested-calendar", plan.CalendarName.Value, false, null);
        string secondListJson = createCalendarListJson(requestedCalendar, createCalendarJson("first-copy", plan.CalendarName.Value + " (2)", false, null));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(createCalendarListJson(requestedCalendar), secondListJson);
        SequencedConflictResolver resolver = new SequencedConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName, ECalendarNameConflictResolution.Cancel);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Cancelled, result.Status);
        Assert.Equal(2, resolver.CallCount);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task RepeatedSuggestedNameRacesStopWithoutMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        string requestedCalendar = createCalendarJson("requested-calendar", plan.CalendarName.Value, false, null);
        string copyTwo = createCalendarJson("copy-two", plan.CalendarName.Value + " (2)", false, null);
        string copyThree = createCalendarJson("copy-three", plan.CalendarName.Value + " (3)", false, null);
        string copyFour = createCalendarJson("copy-four", plan.CalendarName.Value + " (4)", false, null);
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(createCalendarListJson(requestedCalendar), createCalendarListJson(requestedCalendar, copyTwo), createCalendarListJson(requestedCalendar, copyTwo, copyThree), createCalendarListJson(requestedCalendar, copyTwo, copyThree, copyFour));
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver, plan);

        Assert.Equal(EGoogleCalendarExportStatus.Failed, result.Status);
        Assert.Equal(3, resolver.CallCount);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                || request.Method == HttpMethod.Post
                || request.Method == HttpMethod.Delete);
    }
}
