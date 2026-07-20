using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed class GoogleCalendarExportServiceTests
{
    [Fact]
    public async Task MissingLocalBindingRecoversMarkedCalendarInsteadOfCreatingDuplicateAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        RecoveringCalendarHttpMessageHandler handler =
            new RecoveringCalendarHttpMessageHandler(plan.PlanId);
        MemoryBindingStore bindingStore = new MemoryBindingStore();
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(handler));
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            new FixedAccessTokenProvider(),
            apiClient,
            bindingStore))
        {
            GoogleCalendarExportResult result = await exporter.ExportAsync(
                plan,
                CancellationToken.None);

            Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
            Assert.Equal("recovered-calendar", result.CalendarIdOrNull?.Value);
            Assert.Equal("recovered-calendar", bindingStore.CalendarIdOrNull?.Value);
            Assert.Equal(1, result.CreatedEventCount);
            Assert.DoesNotContain(
                handler.Requests,
                request => request.Method == HttpMethod.Post
                    && request.Path.EndsWith("/calendars", StringComparison.Ordinal));
            Assert.Contains(
                handler.Requests,
                request => request.Method == HttpMethod.Get
                    && request.Path.Contains("showHidden=true", StringComparison.Ordinal));
            Assert.Contains(
                handler.Requests,
                request => request.Method == HttpMethod.Put
                    && request.Path.EndsWith(
                        "/calendars/recovered-calendar",
                        StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task BindingForAnotherPlanIsRejectedBeforeCalendarMutationAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        RecoveringCalendarHttpMessageHandler handler =
            new RecoveringCalendarHttpMessageHandler(plan.PlanId);
        MemoryBindingStore bindingStore = new MemoryBindingStore(
            new GoogleCalendarId("wrong-calendar"));
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(handler));
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            new FixedAccessTokenProvider(),
            apiClient,
            bindingStore))
        {
            GoogleCalendarExportResult result = await exporter.ExportAsync(
                plan,
                CancellationToken.None);

            Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
            Assert.Equal("recovered-calendar", bindingStore.CalendarIdOrNull?.Value);
            Assert.DoesNotContain(
                handler.Requests,
                request => request.Method == HttpMethod.Put
                    && request.Path.EndsWith("/wrong-calendar", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task MatchingBoundCalendarSkipsCalendarListScanAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        RecoveringCalendarHttpMessageHandler handler =
            new RecoveringCalendarHttpMessageHandler(plan.PlanId);
        MemoryBindingStore bindingStore = new MemoryBindingStore(
            new GoogleCalendarId("recovered-calendar"));
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            new FixedAccessTokenProvider(),
            new GoogleCalendarApiClient(new HttpClient(handler)),
            bindingStore))
        {
            GoogleCalendarExportResult result = await exporter.ExportAsync(
                plan,
                CancellationToken.None);

            Assert.Equal(EGoogleCalendarExportStatus.Success, result.Status);
            Assert.DoesNotContain(
                handler.Requests,
                request => request.Method == HttpMethod.Get
                    && request.Path.Contains(
                        "/users/me/calendarList?",
                        StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ReconciliationUpdatesDesiredEventsAndDeletesOnlyManagedStaleEventsAsync()
    {
        GoogleCalendarExportPlan plan = createPlan();
        GoogleCalendarEventId desiredId = GoogleCalendarEventId.Create(
            plan.PlanId,
            plan.Events[0].SourceId);
        GoogleCalendarEventId staleId = GoogleCalendarEventId.Create(
            plan.PlanId,
            new GoogleCalendarSourceEventId("stale"));
        ReconciliationHttpMessageHandler handler =
            new ReconciliationHttpMessageHandler(
                plan.PlanId,
                desiredId,
                staleId);
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(handler));

        GoogleCalendarReconciliationResult result = await apiClient.ReconcileEventsAsync(
            new GoogleAccessToken("access-secret"),
            new GoogleCalendarId("calendar-id"),
            plan,
            CancellationToken.None);

        Assert.Equal(0, result.CreatedEventCount);
        Assert.Equal(1, result.UpdatedEventCount);
        Assert.Equal(1, result.DeletedEventCount);
        Assert.Contains(
            handler.Requests,
            request => request.Path.Contains(
                "privateExtendedProperty=timetableGeneratorManaged%3Dtrue",
                StringComparison.Ordinal));
        Assert.Contains(
            handler.Requests,
            request => request.Path.Contains(
                "privateExtendedProperty=timetableGeneratorPlanId%3D",
                StringComparison.Ordinal));
        Assert.Single(
            handler.Requests,
            request => request.Method == HttpMethod.Delete);
        Assert.Contains(
            handler.Requests,
            request => request.Method == HttpMethod.Delete
                && request.Path.EndsWith(staleId.Value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApiTimeoutIsReportedAsNetworkFailureAsync()
    {
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(new TimeoutHttpMessageHandler()));
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            new FixedAccessTokenProvider(),
            apiClient,
            new MemoryBindingStore()))
        {
            GoogleCalendarExportResult result = await exporter.ExportAsync(
                createPlan(),
                CancellationToken.None);

            Assert.Equal(EGoogleCalendarExportStatus.NetworkFailed, result.Status);
            Assert.Equal("google_calendar_timeout", result.DiagnosticCodeOrNull);
        }
    }

    [Fact]
    public async Task ServerRequestTimeoutIsReportedAsNetworkFailureAsync()
    {
        GoogleCalendarExportResult result = await exportWithResponseAsync(
            HttpStatusCode.RequestTimeout,
            "{}");

        Assert.Equal(EGoogleCalendarExportStatus.NetworkFailed, result.Status);
    }

    [Fact]
    public async Task ForbiddenRateLimitIsNotMisreportedAsAccessDeniedAsync()
    {
        GoogleCalendarExportResult result = await exportWithResponseAsync(
            HttpStatusCode.Forbidden,
            "{\"error\":{\"errors\":[{\"reason\":\"rateLimitExceeded\"}]}}");

        Assert.Equal(EGoogleCalendarExportStatus.NetworkFailed, result.Status);
    }

    [Fact]
    public async Task OrdinaryForbiddenResponseRemainsAccessDeniedAsync()
    {
        GoogleCalendarExportResult result = await exportWithResponseAsync(
            HttpStatusCode.Forbidden,
            "{\"error\":{\"errors\":[{\"reason\":\"forbidden\"}]}}");

        Assert.Equal(EGoogleCalendarExportStatus.AccessDenied, result.Status);
    }

    [Fact]
    public async Task RepeatedCalendarListPageTokenIsRejectedAsync()
    {
        RepeatingCalendarPageHttpMessageHandler handler =
            new RepeatingCalendarPageHttpMessageHandler();
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(handler));

        GoogleCalendarApiException exception = await Assert.ThrowsAsync<
            GoogleCalendarApiException>(
            async delegate
            {
                await apiClient.FindPlanCalendarOrNullAsync(
                    new GoogleAccessToken("access-secret"),
                    new PlanId(Guid.NewGuid()),
                    CancellationToken.None);
            });

        Assert.Equal("calendar_list_invalid_pagination", exception.DiagnosticCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task OversizedCalendarListResponseIsRejectedAsync()
    {
        GoogleCalendarApiClient apiClient = new GoogleCalendarApiClient(
            new HttpClient(
                new FixedResponseHttpMessageHandler(
                    HttpStatusCode.OK,
                    new string('x', 5_000_000))));

        GoogleCalendarApiException exception = await Assert.ThrowsAsync<
            GoogleCalendarApiException>(
            async delegate
            {
                await apiClient.FindPlanCalendarOrNullAsync(
                    new GoogleAccessToken("access-secret"),
                    new PlanId(Guid.NewGuid()),
                    CancellationToken.None);
            });

        Assert.Equal("google_calendar_response_too_large", exception.DiagnosticCode);
    }

    [Fact]
    public async Task DisposeDuringActiveExportCancelsBeforeReleasingOwnedResourcesAsync()
    {
        BlockingAccessTokenProvider accessTokenProvider =
            new BlockingAccessTokenProvider();
        TrackingDisposable ownedResources = new TrackingDisposable();
        GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            accessTokenProvider,
            new GoogleCalendarApiClient(new HttpClient(new TimeoutHttpMessageHandler())),
            new MemoryBindingStore(),
            ownedResources);
        Task<GoogleCalendarExportResult> exportTask = exporter.ExportAsync(
            createPlan(),
            CancellationToken.None);
        await accessTokenProvider.Started.WaitAsync(
            TimeSpan.FromSeconds(2.0),
            TestContext.Current.CancellationToken);

        exporter.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async delegate
            {
                await exportTask;
            });
        Assert.Equal(1, ownedResources.DisposeCount);
        exporter.Dispose();
        Assert.Equal(1, ownedResources.DisposeCount);
    }

    [Fact]
    public void ExportResultRejectsInvalidStatusAndNegativeCounts()
    {
        Assert.Throws<ArgumentException>(
            delegate
            {
                GoogleCalendarExportResult.Fail(
                    EGoogleCalendarExportStatus.None,
                    "invalid");
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new GoogleCalendarReconciliationResult(-1, 0, 0);
            });
    }

    private static GoogleCalendarExportPlan createPlan()
    {
        PlanId planId = new PlanId(
            Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423"));
        GoogleCalendarExportEvent exportEvent = new GoogleCalendarExportEvent(
            new GoogleCalendarSourceEventId("course:ITP30003"),
            new CalendarEventContent("컴퓨터 구조(01)", "OH 401", "담당: 이원형"),
            new GoogleCalendarRecurrenceDateRange(
                new DateOnly(2026, 8, 31),
                new DateOnly(2026, 12, 20)),
            new DailyTimeRange(
                new ScheduleTime(11, 30),
                new ScheduleTime(12, 15)),
            new EDay[] { EDay.Monday, EDay.Thursday });
        return new GoogleCalendarExportPlan(
            planId,
            new PlanName("2026-2학기 시간표"),
            new CalendarTimeZoneId("Asia/Seoul"),
            new GoogleCalendarExportEvent[] { exportEvent });
    }

    private static async Task<GoogleCalendarExportResult> exportWithResponseAsync(
        HttpStatusCode statusCode,
        string body)
    {
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(
            new FixedAccessTokenProvider(),
            new GoogleCalendarApiClient(
                new HttpClient(
                    new FixedResponseHttpMessageHandler(statusCode, body))),
            new MemoryBindingStore()))
        {
            return await exporter.ExportAsync(
                createPlan(),
                CancellationToken.None);
        }
    }

    private sealed class FixedAccessTokenProvider : IGoogleAccessTokenProvider
    {
        public Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                GoogleOAuthAuthorizationResult.Complete(
                    new GoogleAccessToken("access-secret")));
        }
    }

    private sealed class BlockingAccessTokenProvider : IGoogleAccessTokenProvider
    {
        private readonly TaskCompletionSource mStartedSource =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started
        {
            get
            {
                return mStartedSource.Task;
            }
        }

        public async Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(
            CancellationToken cancellationToken)
        {
            mStartedSource.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The cancellation wait unexpectedly completed.");
        }
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class MemoryBindingStore : IGoogleCalendarBindingStore
    {
        public GoogleCalendarId? CalendarIdOrNull { get; private set; }

        public MemoryBindingStore()
        {
        }

        public MemoryBindingStore(GoogleCalendarId initialCalendarId)
        {
            CalendarIdOrNull = initialCalendarId;
        }

        public Task<GoogleCalendarId?> GetCalendarIdOrNullAsync(
            PlanId planId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CalendarIdOrNull);
        }

        public Task SaveCalendarIdAsync(
            PlanId planId,
            GoogleCalendarId calendarId,
            CancellationToken cancellationToken)
        {
            CalendarIdOrNull = calendarId;
            return Task.CompletedTask;
        }

        public Task DeleteCalendarIdAsync(
            PlanId planId,
            CancellationToken cancellationToken)
        {
            CalendarIdOrNull = null;
            return Task.CompletedTask;
        }
    }

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("Simulated HTTP timeout."));
        }
    }

    private sealed class FixedResponseHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode mStatusCode;
        private readonly string mBody;

        public FixedResponseHttpMessageHandler(
            HttpStatusCode statusCode,
            string body)
        {
            mStatusCode = statusCode;
            mBody = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new HttpResponseMessage(mStatusCode)
                {
                    Content = new StringContent(mBody, Encoding.UTF8, "application/json"),
                });
        }
    }

    private sealed class RepeatingCalendarPageHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"items\":[],\"nextPageToken\":\"repeated\"}",
                        Encoding.UTF8,
                        "application/json"),
                });
        }
    }

    private sealed class RecoveringCalendarHttpMessageHandler : RecordingHttpMessageHandler
    {
        private readonly PlanId mPlanId;

        public RecoveringCalendarHttpMessageHandler(PlanId planId)
        {
            mPlanId = planId;
        }

        protected override HttpResponseMessage createResponse(RequestRecord request)
        {
            if (request.Path.EndsWith(
                "/users/me/calendarList/wrong-calendar",
                StringComparison.Ordinal))
            {
                return jsonResponse(
                    "{\"description\":\"TimetableGenerator-Plan:"
                        + Guid.Empty.ToString("N")
                        + "\"}");
            }

            if (request.Path.EndsWith(
                "/users/me/calendarList/recovered-calendar",
                StringComparison.Ordinal))
            {
                return jsonResponse(
                    "{\"description\":\""
                        + GoogleCalendarApiClient.createPlanMarker(mPlanId)
                        + "\"}");
            }

            if (request.Path.Contains(
                "/users/me/calendarList?",
                StringComparison.Ordinal))
            {
                return jsonResponse(
                    "{\"items\":[{\"id\":\"recovered-calendar\",\"description\":\""
                        + GoogleCalendarApiClient.createPlanMarker(mPlanId)
                        + "\"}]}");
            }

            if (request.Method == HttpMethod.Get
                && request.Path.Contains("/events?", StringComparison.Ordinal))
            {
                return jsonResponse("{\"items\":[]}");
            }

            return jsonResponse("{}");
        }
    }

    private sealed class ReconciliationHttpMessageHandler : RecordingHttpMessageHandler
    {
        private readonly GoogleCalendarEventId mDesiredId;
        private readonly GoogleCalendarEventId mStaleId;
        private readonly PlanId mPlanId;

        public ReconciliationHttpMessageHandler(
            PlanId planId,
            GoogleCalendarEventId desiredId,
            GoogleCalendarEventId staleId)
        {
            mPlanId = planId;
            mDesiredId = desiredId;
            mStaleId = staleId;
        }

        protected override HttpResponseMessage createResponse(RequestRecord request)
        {
            if (request.Method == HttpMethod.Get)
            {
                return jsonResponse(
                    "{\"items\":[{\"id\":\""
                        + mDesiredId.Value
                        + "\",\"extendedProperties\":{\"private\":{"
                        + "\"timetableGeneratorManaged\":\"true\","
                        + "\"timetableGeneratorPlanId\":\""
                        + mPlanId.Value.ToString("N")
                        + "\"}}},{\"id\":\""
                        + mStaleId.Value
                        + "\",\"extendedProperties\":{\"private\":{"
                        + "\"timetableGeneratorManaged\":\"true\","
                        + "\"timetableGeneratorPlanId\":\""
                        + mPlanId.Value.ToString("N")
                        + "\"}}},{\"id\":\"abcde\","
                        + "\"extendedProperties\":{\"private\":{"
                        + "\"timetableGeneratorManaged\":\"true\"}}},"
                        + "{\"id\":\"fghij\",\"extendedProperties\":{"
                        + "\"private\":{\"timetableGeneratorPlanId\":\""
                        + mPlanId.Value.ToString("N")
                        + "\"}}},{\"id\":\"klmno\"},"
                        + "{\"id\":\"pqrst\",\"extendedProperties\":{"
                        + "\"private\":{\"timetableGeneratorManaged\":\"true\","
                        + "\"timetableGeneratorPlanId\":"
                        + "\"11111111111111111111111111111111\"}}}]}");
            }

            return jsonResponse("{}");
        }
    }

    private abstract class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<RequestRecord> mRequests = new List<RequestRecord>();

        public IReadOnlyList<RequestRecord> Requests
        {
            get
            {
                return mRequests;
            }
        }

        protected sealed override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            string path = request.RequestUri == null
                ? string.Empty
                : request.RequestUri.PathAndQuery;
            RequestRecord record = new RequestRecord(
                request.Method,
                path,
                body);
            mRequests.Add(record);
            return createResponse(record);
        }

        protected abstract HttpResponseMessage createResponse(RequestRecord request);

        protected static HttpResponseMessage jsonResponse(string json)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed record RequestRecord(
        HttpMethod Method,
        string Path,
        string Body);
}
