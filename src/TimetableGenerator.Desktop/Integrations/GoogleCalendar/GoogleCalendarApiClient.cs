using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarApiClient
{
    private const int MAXIMUM_CALENDAR_LIST_PAGE_COUNT = 64;
    private const int MAXIMUM_CALENDAR_LIST_PAGE_SIZE = 250;
    private const long MAXIMUM_RESPONSE_BODY_BYTES = 4_194_304L;
    private const int MAXIMUM_EVENT_LIST_PAGE_COUNT = 64;
    private const int MAXIMUM_EVENT_LIST_PAGE_SIZE = 2_500;

    private static readonly Uri API_ROOT = new Uri("https://www.googleapis.com/calendar/v3/", UriKind.Absolute);

    private readonly HttpClient mHttpClient;

    public GoogleCalendarApiClient(HttpClient httpClient)
    {
        if (httpClient == null)
        {
            throw new ArgumentNullException(nameof(httpClient));
        }

        mHttpClient = httpClient;
    }

    public async Task<IReadOnlyList<GoogleCalendarDescriptor>> ListCalendarsAsync(
        GoogleAccessToken accessToken,
        CancellationToken cancellationToken)
    {
        List<GoogleCalendarDescriptor> calendars = new List<GoogleCalendarDescriptor>();
        GoogleCalendarPaginationGuard paginationGuard =
            new GoogleCalendarPaginationGuard(
                MAXIMUM_CALENDAR_LIST_PAGE_COUNT,
                "calendar_list_invalid_pagination");
        string? pageTokenOrNull = null;
        do
        {
            paginationGuard.BeginPage();
            string relativeUri = "users/me/calendarList?maxResults="
                + MAXIMUM_CALENDAR_LIST_PAGE_SIZE.ToString(CultureInfo.InvariantCulture)
                + "&showHidden=true";
            if (pageTokenOrNull != null)
            {
                relativeUri += "&pageToken=" + Uri.EscapeDataString(pageTokenOrNull);
            }

            using (HttpRequestMessage request = createRequest(
                HttpMethod.Get,
                relativeUri,
                accessToken))
            {
                using (HttpResponseMessage response = await sendAsync(
                    request,
                    cancellationToken).ConfigureAwait(false))
                {
                    await ensureSuccessAsync(response, "calendar_list_failed", cancellationToken).ConfigureAwait(false);
                    using (JsonDocument document = await readJsonAsync(
                        response,
                        cancellationToken).ConfigureAwait(false))
                    {
                        JsonElement items;
                        if (document.RootElement.TryGetProperty("items", out items)
                            && items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement item in items.EnumerateArray())
                            {
                                if (getBooleanOrDefault(item, "deleted"))
                                {
                                    continue;
                                }

                                string? descriptionOrNull = getStringOrNull(item, "description");
                                string? idOrNull = getStringOrNull(item, "id");
                                string? summaryOverrideOrNull = getStringOrNull(item, "summaryOverride");
                                string? summaryOrNull = getStringOrNull(item, "summary");
                                string? displayNameOrNull =
                                    string.IsNullOrWhiteSpace(summaryOverrideOrNull)
                                        ? summaryOrNull
                                        : summaryOverrideOrNull;
                                if (string.IsNullOrWhiteSpace(idOrNull) || string.IsNullOrWhiteSpace(displayNameOrNull))
                                {
                                    continue;
                                }

                                calendars.Add(
                                    new GoogleCalendarDescriptor(
                                        new GoogleCalendarId(idOrNull),
                                        displayNameOrNull,
                                        getBooleanOrDefault(item, "primary"),
                                        tryParseManagedPlanIdOrNull(
                                            descriptionOrNull),
                                        parseAccessRole(item)));
                            }
                        }

                        pageTokenOrNull = paginationGuard.AcceptNextPageTokenOrNull(
                            getStringOrNull(
                                document.RootElement,
                                "nextPageToken"));
                    }
                }
            }
        }
        while (pageTokenOrNull != null);

        return calendars.AsReadOnly();
    }

    public async Task<GoogleCalendarId> CreatePlanCalendarAsync(
        GoogleAccessToken accessToken,
        GoogleCalendarExportPlan plan,
        CancellationToken cancellationToken)
    {
        JsonObject resource = createCalendarResource(plan);
        using (HttpRequestMessage request = createJsonRequest(
            HttpMethod.Post,
            "calendars",
            accessToken,
            resource))
        {
            using (HttpResponseMessage response = await sendAsync(
                request,
                cancellationToken).ConfigureAwait(false))
            {
                await ensureSuccessAsync(
                    response,
                    "calendar_create_failed",
                    cancellationToken).ConfigureAwait(false);
                using (JsonDocument document = await readJsonAsync(
                    response,
                    cancellationToken).ConfigureAwait(false))
                {
                    string? idOrNull = getStringOrNull(document.RootElement, "id");
                    if (string.IsNullOrWhiteSpace(idOrNull))
                    {
                        throw new GoogleCalendarApiException(HttpStatusCode.OK, "calendar_id_missing");
                    }

                    return new GoogleCalendarId(idOrNull);
                }
            }
        }
    }

    public async Task UpdatePlanCalendarAsync(
        GoogleAccessToken accessToken,
        GoogleCalendarId calendarId,
        GoogleCalendarExportPlan plan,
        CancellationToken cancellationToken)
    {
        JsonObject resource = createCalendarResource(plan);
        using (HttpRequestMessage request = createJsonRequest(
            HttpMethod.Put,
            "calendars/" + escapePathSegment(calendarId.Value),
            accessToken,
            resource))
        {
            using (HttpResponseMessage response = await sendAsync(
                request,
                cancellationToken).ConfigureAwait(false))
            {
                await ensureSuccessAsync(
                    response,
                    "calendar_update_failed",
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<GoogleCalendarReconciliationResult> ReconcileEventsAsync(
        GoogleAccessToken accessToken,
        GoogleCalendarId calendarId,
        GoogleCalendarExportPlan plan,
        CancellationToken cancellationToken)
    {
        HashSet<GoogleCalendarEventId> existingEventIds = await listManagedEventIdsAsync(
            accessToken,
            calendarId,
            plan.PlanId,
            cancellationToken).ConfigureAwait(false);
        HashSet<GoogleCalendarEventId> desiredEventIds = new HashSet<GoogleCalendarEventId>();
        int createdEventCount = 0;
        int updatedEventCount = 0;
        foreach (GoogleCalendarExportEvent exportEvent in plan.Events)
        {
            GoogleCalendarEventId eventId = GoogleCalendarEventId.Create(plan.PlanId, exportEvent.SourceId);
            desiredEventIds.Add(eventId);
            JsonObject resource = GoogleCalendarEventResourceFactory.Create(
                plan.PlanId,
                plan.TimeZoneId,
                exportEvent);
            if (existingEventIds.Contains(eventId))
            {
                await updateEventAsync(
                    accessToken,
                    calendarId,
                    eventId,
                    resource,
                    cancellationToken).ConfigureAwait(false);
                updatedEventCount++;
            }
            else
            {
                await createEventAsync(accessToken, calendarId, resource, cancellationToken).ConfigureAwait(false);
                createdEventCount++;
            }
        }

        int deletedEventCount = 0;
        foreach (GoogleCalendarEventId existingEventId in existingEventIds)
        {
            if (desiredEventIds.Contains(existingEventId))
            {
                continue;
            }

            await deleteEventAsync(
                accessToken,
                calendarId,
                existingEventId,
                cancellationToken).ConfigureAwait(false);
            deletedEventCount++;
        }

        return new GoogleCalendarReconciliationResult(
            createdEventCount,
            updatedEventCount,
            deletedEventCount);
    }

    internal static string createPlanMarker(PlanId planId)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException("Google Calendar markers require a valid plan ID.", nameof(planId));
        }

        return "TimetableGenerator-Plan:" + planId.Value.ToString("N");
    }

    private async Task<HashSet<GoogleCalendarEventId>> listManagedEventIdsAsync(
        GoogleAccessToken accessToken,
        GoogleCalendarId calendarId,
        PlanId planId,
        CancellationToken cancellationToken)
    {
        HashSet<GoogleCalendarEventId> eventIds = new HashSet<GoogleCalendarEventId>();
        GoogleCalendarPaginationGuard paginationGuard =
            new GoogleCalendarPaginationGuard(
                MAXIMUM_EVENT_LIST_PAGE_COUNT,
                "event_list_invalid_pagination");
        string? pageTokenOrNull = null;
        do
        {
            paginationGuard.BeginPage();
            string relativeUri = "calendars/"
                + escapePathSegment(calendarId.Value)
                + "/events?maxResults="
                + MAXIMUM_EVENT_LIST_PAGE_SIZE.ToString(CultureInfo.InvariantCulture)
                + "&showDeleted=false&singleEvents=false&privateExtendedProperty="
                + Uri.EscapeDataString(
                    GoogleCalendarEventResourceFactory.CreateManagedPropertyFilter())
                + "&privateExtendedProperty="
                + Uri.EscapeDataString(
                    GoogleCalendarEventResourceFactory.CreatePlanPropertyFilter(
                        planId));
            if (pageTokenOrNull != null)
            {
                relativeUri += "&pageToken=" + Uri.EscapeDataString(pageTokenOrNull);
            }

            using (HttpRequestMessage request = createRequest(
                HttpMethod.Get,
                relativeUri,
                accessToken))
            {
                using (HttpResponseMessage response = await sendAsync(
                    request,
                    cancellationToken).ConfigureAwait(false))
                {
                    await ensureSuccessAsync(response, "event_list_failed", cancellationToken).ConfigureAwait(false);
                    using (JsonDocument document = await readJsonAsync(
                        response,
                        cancellationToken).ConfigureAwait(false))
                    {
                        JsonElement items;
                        if (document.RootElement.TryGetProperty("items", out items)
                            && items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement item in items.EnumerateArray())
                            {
                                if (GoogleCalendarEventResourceFactory.isManagedByPlan(
                                        item,
                                        planId) == false)
                                {
                                    continue;
                                }

                                string? idOrNull = getStringOrNull(item, "id");
                                if (string.IsNullOrWhiteSpace(idOrNull) == false)
                                {
                                    eventIds.Add(GoogleCalendarEventId.createFromExisting(idOrNull));
                                }
                            }
                        }

                        pageTokenOrNull = paginationGuard.AcceptNextPageTokenOrNull(
                            getStringOrNull(
                                document.RootElement,
                                "nextPageToken"));
                    }
                }
            }
        }
        while (pageTokenOrNull != null);

        return eventIds;
    }

    private async Task createEventAsync(
        GoogleAccessToken accessToken,
        GoogleCalendarId calendarId,
        JsonObject resource,
        CancellationToken cancellationToken)
    {
        using (HttpRequestMessage request = createJsonRequest(
            HttpMethod.Post,
            "calendars/" + escapePathSegment(calendarId.Value) + "/events",
            accessToken,
            resource))
        {
            using (HttpResponseMessage response = await sendAsync(
                request,
                cancellationToken).ConfigureAwait(false))
            {
                await ensureSuccessAsync(response, "event_create_failed", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task updateEventAsync(
        GoogleAccessToken accessToken,
        GoogleCalendarId calendarId,
        GoogleCalendarEventId eventId,
        JsonObject resource,
        CancellationToken cancellationToken)
    {
        using (HttpRequestMessage request = createJsonRequest(
            HttpMethod.Put,
            "calendars/"
                + escapePathSegment(calendarId.Value)
                + "/events/"
                + escapePathSegment(eventId.Value),
            accessToken,
            resource))
        {
            using (HttpResponseMessage response = await sendAsync(
                request,
                cancellationToken).ConfigureAwait(false))
            {
                await ensureSuccessAsync(response, "event_update_failed", cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task deleteEventAsync(
        GoogleAccessToken accessToken,
        GoogleCalendarId calendarId,
        GoogleCalendarEventId eventId,
        CancellationToken cancellationToken)
    {
        using (HttpRequestMessage request = createRequest(
            HttpMethod.Delete,
            "calendars/"
                + escapePathSegment(calendarId.Value)
                + "/events/"
                + escapePathSegment(eventId.Value),
            accessToken))
        {
            using (HttpResponseMessage response = await sendAsync(
                request,
                cancellationToken).ConfigureAwait(false))
            {
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    await ensureSuccessAsync(response, "event_delete_failed", cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static JsonObject createCalendarResource(GoogleCalendarExportPlan plan)
    {
        return new JsonObject
        {
            ["summary"] = plan.CalendarName.Value,
            ["description"] = createPlanMarker(plan.PlanId),
            ["timeZone"] = plan.TimeZoneId.Value,
        };
    }

    private static HttpRequestMessage createJsonRequest(
        HttpMethod method,
        string relativeUri,
        GoogleAccessToken accessToken,
        JsonObject resource)
    {
        HttpRequestMessage request = createRequest(method, relativeUri, accessToken);
        request.Content = new StringContent(resource.ToJsonString(), Encoding.UTF8, "application/json");
        return request;
    }

    private static HttpRequestMessage createRequest(
        HttpMethod method,
        string relativeUri,
        GoogleAccessToken accessToken)
    {
        if (accessToken == null)
        {
            throw new ArgumentNullException(nameof(accessToken));
        }

        HttpRequestMessage request = new HttpRequestMessage(method, new Uri(API_ROOT, relativeUri));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Value);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<HttpResponseMessage> sendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return await mHttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<JsonDocument> readJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            byte[] content = await GoogleHttpResponseBodyReader.ReadAsync(
                response.Content,
                MAXIMUM_RESPONSE_BODY_BYTES,
                cancellationToken).ConfigureAwait(false);
            return JsonDocument.Parse(content);
        }
        catch (GoogleHttpResponseBodyLimitExceededException)
        {
            throw new GoogleCalendarApiException(response.StatusCode, "google_calendar_response_too_large");
        }
        catch (IOException exception)
        {
            throw new HttpRequestException("The Google Calendar response body could not be read.", exception);
        }
        catch (JsonException)
        {
            throw new GoogleCalendarApiException(response.StatusCode, "google_calendar_invalid_response");
        }
    }

    private static async Task ensureSuccessAsync(
        HttpResponseMessage response,
        string diagnosticCode,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        bool isTransient = isTransientStatusCode(response.StatusCode);
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            isTransient = await containsRateLimitReasonAsync(response, cancellationToken).ConfigureAwait(false);
        }

        throw new GoogleCalendarApiException(
            response.StatusCode,
            diagnosticCode,
            isTransient
                ? EGoogleCalendarApiFailureKind.Transient
                : EGoogleCalendarApiFailureKind.Permanent);
    }

    private static async Task<bool> containsRateLimitReasonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        byte[] content;
        try
        {
            content = await GoogleHttpResponseBodyReader.ReadAsync(
                response.Content,
                MAXIMUM_RESPONSE_BODY_BYTES,
                cancellationToken).ConfigureAwait(false);
        }
        catch (GoogleHttpResponseBodyLimitExceededException)
        {
            return false;
        }
        catch (IOException exception)
        {
            throw new HttpRequestException(
                "The Google Calendar error response body could not be read.",
                exception);
        }

        try
        {
            using (JsonDocument document = JsonDocument.Parse(content))
            {
                JsonElement error;
                JsonElement errors;
                if (document.RootElement.TryGetProperty("error", out error) == false
                    || error.ValueKind != JsonValueKind.Object
                    || error.TryGetProperty("errors", out errors) == false
                    || errors.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (JsonElement errorDetail in errors.EnumerateArray())
                {
                    string? reasonOrNull = getStringOrNull(errorDetail, "reason");
                    if (string.Equals(
                        reasonOrNull,
                        "userRateLimitExceeded",
                        StringComparison.Ordinal)
                        || string.Equals(
                            reasonOrNull,
                            "rateLimitExceeded",
                            StringComparison.Ordinal)
                        || string.Equals(
                            reasonOrNull,
                            "quotaExceeded",
                            StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool isTransientStatusCode(HttpStatusCode statusCode)
    {
        int numericStatusCode = (int)statusCode;
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || numericStatusCode >= 500;
    }

    private static string escapePathSegment(string value)
    {
        return Uri.EscapeDataString(value);
    }

    private static string? getStringOrNull(JsonElement element, string propertyName)
    {
        JsonElement property;
        if (element.ValueKind != JsonValueKind.Object
            || element.TryGetProperty(propertyName, out property) == false
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool getBooleanOrDefault(JsonElement element, string propertyName)
    {
        JsonElement property;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static EGoogleCalendarAccessRole parseAccessRole(JsonElement calendarListEntry)
    {
        string? accessRoleOrNull = getStringOrNull(calendarListEntry, "accessRole");
        return accessRoleOrNull switch
        {
            "freeBusyReader" => EGoogleCalendarAccessRole.FreeBusyReader,
            "reader" => EGoogleCalendarAccessRole.Reader,
            "writer" => EGoogleCalendarAccessRole.Writer,
            "writerWithoutPrivateAccess" =>
                EGoogleCalendarAccessRole.WriterWithoutPrivateAccess,
            "owner" => EGoogleCalendarAccessRole.Owner,
            null => EGoogleCalendarAccessRole.None,
            _ => EGoogleCalendarAccessRole.None,
        };
    }

    private static PlanId? tryParseManagedPlanIdOrNull(string? descriptionOrNull)
    {
        const string MARKER_PREFIX = "TimetableGenerator-Plan:";
        if (descriptionOrNull == null
            || descriptionOrNull.StartsWith(
                MARKER_PREFIX,
                StringComparison.Ordinal) == false)
        {
            return null;
        }

        Guid planIdValue;
        return Guid.TryParseExact(
                descriptionOrNull[MARKER_PREFIX.Length..],
                "N",
                out planIdValue)
            && planIdValue != Guid.Empty
                ? new PlanId(planIdValue)
                : null;
    }

}
