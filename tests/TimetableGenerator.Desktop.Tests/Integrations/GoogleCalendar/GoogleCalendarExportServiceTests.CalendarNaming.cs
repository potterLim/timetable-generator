using System;
using System.Net.Http;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    [Fact]
    public async Task AvailableRequestedNameCreatesANewCalendarWithoutPromptingAsync()
    {
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler("{\"items\":[]}");
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.Equal("created-calendar", result.CalendarIdOrNull?.Value);
        Assert.Equal("2026-2학기 시간표", result.CalendarNameOrNull?.Value);
        Assert.Equal(0, resolver.CallCount);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post
                && request.Path.EndsWith("/calendars", StringComparison.Ordinal)
                && hasCalendarSummary(request, "2026-2학기 시간표"));
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                && request.Path.EndsWith("/calendars/created-calendar", StringComparison.Ordinal)
                && hasCalendarDescription(request, "한동대학교 2026-2 시간표입니다."));
    }

    [Fact]
    public async Task NameConflictCanCreateTheNextAvailableNameAsync()
    {
        string listJson = createCalendarListJson(createCalendarJson("existing", "2026-2학기 시간표", false, null));
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler(listJson, listJson);
        RecordingConflictResolver resolver = new RecordingConflictResolver(ECalendarNameConflictResolution.CreateWithAvailableName);

        GoogleCalendarExportResult result = await exportAsync(handler, resolver);

        Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
        Assert.Equal("2026-2학기 시간표 (2)", result.CalendarNameOrNull?.Value);
        Assert.Equal(1, resolver.CallCount);
        Assert.False(resolver.ConflictOrNull?.CanReplace);
        Assert.Equal("2026-2학기 시간표 (2)", resolver.ConflictOrNull?.NextAvailableName.Value);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Post
                && request.Path.EndsWith("/calendars", StringComparison.Ordinal)
                && hasCalendarSummary(request, "2026-2학기 시간표 (2)"));
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                && request.Path.EndsWith("/calendars/created-calendar", StringComparison.Ordinal)
                && hasCalendarDescription(request, "한동대학교 2026-2 시간표입니다."));
    }
}
