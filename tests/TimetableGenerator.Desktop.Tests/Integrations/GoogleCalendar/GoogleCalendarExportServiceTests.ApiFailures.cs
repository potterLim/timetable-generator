using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Planning;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    [Fact]
    public async Task CalendarListUsesVisibleOverrideAndSkipsDeletedEntriesAsync()
    {
        string json = "{\"items\":[" + "{\"id\":\"deleted\",\"summary\":\"Deleted\",\"deleted\":true}," + "{\"id\":\"visible\",\"summary\":\"Original\"," + "\"summaryOverride\":\"Visible\",\"primary\":false," + "\"accessRole\":\"owner\"," + "\"description\":\"TimetableGenerator-Plan:" + Guid.NewGuid().ToString("N") + "\"}]}";
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(new HttpClient(new FixedResponseHttpMessageHandler(HttpStatusCode.OK, json)));

        IReadOnlyList<GoogleCalendarDescriptor> calendars = await apiClient.ListCalendarsAsync(new GoogleAccessToken("access-secret"), CancellationToken.None);

        GoogleCalendarDescriptor calendar = Assert.Single(calendars);
        Assert.Equal("visible", calendar.CalendarId.Value);
        Assert.Equal("Visible", calendar.DisplayName);
        Assert.Equal(EGoogleCalendarAccessRole.Owner, calendar.AccessRole);
        Assert.NotNull(calendar.ManagedPlanIdOrNull);
        Assert.True(calendar.CanReplace);
    }

    [Fact]
    public async Task ApiTimeoutIsReportedAsNetworkFailureAsync()
    {
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(new FixedAccessTokenProvider(), new GoogleCalendarApiClient(new HttpClient(new TimeoutHttpMessageHandler()))))
        {
            GoogleCalendarExportResult result = await exporter.ExportAsync(createPlan(), new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel), CancellationToken.None);

            Assert.Equal(EGoogleCalendarExportStatus.NetworkFailed, result.Status);
            Assert.Equal("google_calendar_timeout", result.DiagnosticCodeOrNull);
        }
    }

    [Fact]
    public async Task ForbiddenRateLimitIsNotMisreportedAsAccessDeniedAsync()
    {
        GoogleCalendarExportResult result = await exportWithResponseAsync(HttpStatusCode.Forbidden, "{\"error\":{\"errors\":[{\"reason\":\"rateLimitExceeded\"}]}}");

        Assert.Equal(EGoogleCalendarExportStatus.NetworkFailed, result.Status);
    }

    [Fact]
    public async Task OrdinaryForbiddenResponseRemainsAccessDeniedAsync()
    {
        GoogleCalendarExportResult result = await exportWithResponseAsync(HttpStatusCode.Forbidden, "{\"error\":{\"errors\":[{\"reason\":\"forbidden\"}]}}");

        Assert.Equal(EGoogleCalendarExportStatus.AccessDenied, result.Status);
    }

    [Fact]
    public async Task OrdinaryForbiddenManagedCalendarProbeIsTreatedAsUnmanagedAsync()
    {
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(new HttpClient(new FixedResponseHttpMessageHandler(HttpStatusCode.Forbidden, "{\"error\":{\"errors\":[{\"reason\":\"forbidden\"}]}}")));

        PlanId? managedPlanIdOrNull = await apiClient.FindManagedPlanIdAsync(new GoogleAccessToken("access-secret"), new GoogleCalendarId("calendar-id"), CancellationToken.None);

        Assert.Null(managedPlanIdOrNull);
    }

    [Fact]
    public async Task RateLimitedManagedCalendarProbeRemainsTransientAsync()
    {
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(new HttpClient(new FixedResponseHttpMessageHandler(HttpStatusCode.Forbidden, "{\"error\":{\"errors\":[{\"reason\":\"rateLimitExceeded\"}]}}")));

        GoogleCalendarApiException exception = await Assert.ThrowsAsync<GoogleCalendarApiException>(
            async delegate
            {
                await apiClient.FindManagedPlanIdAsync(new GoogleAccessToken("access-secret"), new GoogleCalendarId("calendar-id"), CancellationToken.None);
            });

        Assert.Equal(EGoogleCalendarApiFailureKind.Transient, exception.FailureKind);
        Assert.Equal("managed_calendar_probe_failed", exception.DiagnosticCode);
    }

    [Fact]
    public async Task EventFailureDoesNotPublishFriendlyCalendarDescriptionAsync()
    {
        CalendarExportHttpMessageHandler handler = new CalendarExportHttpMessageHandler("{\"items\":[]}")
        {
            EventMutationFailureStatusCodeOrNull = HttpStatusCode.ServiceUnavailable,
        };

        GoogleCalendarExportResult result = await exportAsync(handler, new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel));

        Assert.Equal(EGoogleCalendarExportStatus.NetworkFailed, result.Status);
        Assert.DoesNotContain(
            handler.Requests,
            request => request.Method == HttpMethod.Put
                && request.Path.EndsWith("/calendars/created-calendar", StringComparison.Ordinal)
                && hasCalendarDescription(request, "한동대학교 2026-2 시간표입니다."));
    }

    [Fact]
    public async Task RepeatedCalendarListPageTokenIsRejectedAsync()
    {
        RepeatingCalendarPageHttpMessageHandler handler = new RepeatingCalendarPageHttpMessageHandler();
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(new HttpClient(handler));

        GoogleCalendarApiException exception = await Assert.ThrowsAsync<GoogleCalendarApiException>(
            async delegate
            {
                await apiClient.ListCalendarsAsync(new GoogleAccessToken("access-secret"), CancellationToken.None);
            });

        Assert.Equal("calendar_list_invalid_pagination", exception.DiagnosticCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task OversizedCalendarListResponseIsRejectedAsync()
    {
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(new HttpClient(new FixedResponseHttpMessageHandler(HttpStatusCode.OK, new string('x', 5_000_000))));

        GoogleCalendarApiException exception = await Assert.ThrowsAsync<GoogleCalendarApiException>(
            async delegate
            {
                await apiClient.ListCalendarsAsync(new GoogleAccessToken("access-secret"), CancellationToken.None);
            });

        Assert.Equal("google_calendar_response_too_large", exception.DiagnosticCode);
    }
}
