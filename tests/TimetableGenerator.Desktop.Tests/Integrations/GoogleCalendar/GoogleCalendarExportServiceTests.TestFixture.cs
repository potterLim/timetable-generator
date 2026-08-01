using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarExportServiceTests
{
    private static async Task<GoogleCalendarExportResult> exportAsync(CalendarExportHttpMessageHandler handler, ICalendarNameConflictResolver resolver, GoogleCalendarExportPlan? planOrNull = null)
    {
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(new FixedAccessTokenProvider(), new GoogleCalendarApiClient(new HttpClient(handler))))
        {
            GoogleCalendarExportPlan plan;
            if (planOrNull == null)
            {
                plan = createPlan();
            }
            else
            {
                plan = planOrNull;
            }

            return await exporter.ExportAsync(plan, resolver, CancellationToken.None);
        }
    }

    private static async Task<GoogleCalendarExportResult> exportWithResponseAsync(HttpStatusCode statusCode, string body)
    {
        using (GoogleCalendarExportService exporter = new GoogleCalendarExportService(new FixedAccessTokenProvider(), new GoogleCalendarApiClient(new HttpClient(new FixedResponseHttpMessageHandler(statusCode, body)))))
        {
            return await exporter.ExportAsync(createPlan(), new RecordingConflictResolver(ECalendarNameConflictResolution.Cancel), CancellationToken.None);
        }
    }

    private static GoogleCalendarExportPlan createPlan()
    {
        PlanId planId = new PlanId(Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423"));
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
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse("2026-2"),
            new CalendarTimeZoneId("Asia/Seoul"),
            new GoogleCalendarExportEvent[] { exportEvent });
    }

    private static GoogleCalendarExportPlan createPlanWithTwoEvents()
    {
        GoogleCalendarExportPlan firstEventPlan = createPlan();
        GoogleCalendarExportEvent secondEvent = new GoogleCalendarExportEvent(
            new GoogleCalendarSourceEventId("course:ITP30004"),
            new CalendarEventContent(
                "운영체제(01)",
                "OH 402",
                "담당: 김교수"),
            new GoogleCalendarRecurrenceDateRange(
                new DateOnly(2026, 8, 31),
                new DateOnly(2026, 12, 20)),
            new DailyTimeRange(
                new ScheduleTime(14, 0),
                new ScheduleTime(15, 15)),
            new EDay[] { EDay.Monday, EDay.Friday });
        return new GoogleCalendarExportPlan(
            firstEventPlan.PlanId,
            firstEventPlan.CalendarName,
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse("2026-2"),
            firstEventPlan.TimeZoneId,
            new GoogleCalendarExportEvent[]
            {
                firstEventPlan.Events[0],
                secondEvent,
            });
    }

    private static string createCalendarListJson(params string[] items)
    {
        return "{\"items\":[" + string.Join(',', items) + "]}";
    }

    private static bool hasCalendarSummary(RequestRecord request, string expectedSummary)
    {
        using (JsonDocument document = JsonDocument.Parse(request.Body))
        {
            JsonElement summary;
            return document.RootElement.TryGetProperty("summary", out summary)
                && summary.ValueKind == JsonValueKind.String
                && string.Equals(summary.GetString(), expectedSummary, StringComparison.Ordinal);
        }
    }

    private static bool hasCalendarDescription(RequestRecord request, string expectedDescription)
    {
        using (JsonDocument document = JsonDocument.Parse(request.Body))
        {
            JsonElement description;
            return document.RootElement.TryGetProperty("description", out description)
                && description.ValueKind == JsonValueKind.String
                && string.Equals(description.GetString(), expectedDescription, StringComparison.Ordinal);
        }
    }

    private static string createCalendarJson(string id, string name, bool isPrimary, string? descriptionOrNull)
    {
        return createCalendarJson(id, name, isPrimary, descriptionOrNull, "owner");
    }

    private static string createCalendarJson(
        string id,
        string name,
        bool isPrimary,
        string? descriptionOrNull,
        string? accessRoleOrNull)
    {
        string description = descriptionOrNull == null ? string.Empty : ",\"description\":\"" + descriptionOrNull + "\"";
        string accessRole = accessRoleOrNull == null ? string.Empty : ",\"accessRole\":\"" + accessRoleOrNull + "\"";
        return "{\"id\":\"" + id + "\",\"summary\":\"" + name + "\",\"primary\":" + (isPrimary ? "true" : "false") + description + accessRole + "}";
    }
}
