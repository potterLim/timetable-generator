using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    private sealed class BlockingAccessTokenProvider : IGoogleAccessTokenProvider
    {
        private readonly TaskCompletionSource mStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started
        {
            get
            {
                return mStartedSource.Task;
            }
        }

        public async Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(CancellationToken cancellationToken)
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

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(new TaskCanceledException("Simulated HTTP timeout."));
        }
    }

    private sealed class FixedResponseHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode mStatusCode;
        private readonly string mBody;

        public FixedResponseHttpMessageHandler(HttpStatusCode statusCode, string body)
        {
            mStatusCode = statusCode;
            mBody = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(mStatusCode)
            {
                Content = new StringContent(mBody, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class RepeatingCalendarPageHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"items\":[],\"nextPageToken\":\"repeated\"}", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class CalendarExportHttpMessageHandler : RecordingHttpMessageHandler
    {
        private readonly Queue<string> mCalendarLists;
        private string mLastCalendarList;

        public string EventListJson { get; init; } = "{\"items\":[]}";

        public HttpStatusCode? EventMutationFailureStatusCodeOrNull { get; init; }

        public CalendarExportHttpMessageHandler(params string[] calendarLists)
        {
            if (calendarLists == null || calendarLists.Length == 0)
            {
                throw new ArgumentException("At least one calendar-list response is required.", nameof(calendarLists));
            }

            mCalendarLists = new Queue<string>(calendarLists);
            mLastCalendarList = calendarLists[^1];
        }

        protected override HttpResponseMessage createResponse(RequestRecord request)
        {
            if (request.Method == HttpMethod.Get && request.Path.Contains("/users/me/calendarList?", StringComparison.Ordinal))
            {
                if (mCalendarLists.Count > 0)
                {
                    mLastCalendarList = mCalendarLists.Dequeue();
                }

                return jsonResponse(mLastCalendarList);
            }

            if (request.Method == HttpMethod.Post && request.Path.EndsWith("/calendars", StringComparison.Ordinal))
            {
                return jsonResponse("{\"id\":\"created-calendar\"}");
            }

            if (request.Method == HttpMethod.Get && request.Path.Contains("/events?", StringComparison.Ordinal))
            {
                return jsonResponse(EventListJson);
            }

            if (request.Path.Contains("/events", StringComparison.Ordinal) && EventMutationFailureStatusCodeOrNull.HasValue)
            {
                return new HttpResponseMessage(EventMutationFailureStatusCodeOrNull.Value)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                };
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

        protected sealed override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body;
            if (request.Content == null)
            {
                body = string.Empty;
            }
            else
            {
                body = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            string path;
            if (request.RequestUri == null)
            {
                path = string.Empty;
            }
            else
            {
                path = request.RequestUri.PathAndQuery;
            }
            RequestRecord record = new RequestRecord(request.Method, path, body);
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

    private sealed record RequestRecord(HttpMethod Method, string Path, string Body);
}
