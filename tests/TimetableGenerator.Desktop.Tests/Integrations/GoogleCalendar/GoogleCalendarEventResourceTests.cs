using System;
using System.Text.Json.Nodes;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed class GoogleCalendarEventResourceTests
{
    [Fact]
    public void ResourceUsesNormalizedIanaIdAndGroupsWeekdays()
    {
        PlanId planId = new PlanId(
            Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423"));
        GoogleCalendarExportEvent exportEvent = createEvent("course:ITP30003");
        TimeZoneInfo windowsTimeZone = TimeZoneInfo.CreateCustomTimeZone(
            "Korea Standard Time",
            TimeSpan.FromHours(9.0),
            "Korea Standard Time",
            "Korea Standard Time");
        CalendarTimeZoneId timeZoneId =
            CalendarTimeZoneId.CreateFromSystemTimeZone(windowsTimeZone);

        JsonObject resource = GoogleCalendarEventResourceFactory.Create(
            planId,
            timeZoneId,
            exportEvent);
        JsonObject start = Assert.IsType<JsonObject>(resource["start"]);
        JsonValue startDateTime = Assert.IsAssignableFrom<JsonValue>(start["dateTime"]);
        JsonValue startTimeZone = Assert.IsAssignableFrom<JsonValue>(start["timeZone"]);
        JsonArray recurrence = Assert.IsType<JsonArray>(resource["recurrence"]);
        JsonValue recurrenceRule = Assert.IsAssignableFrom<JsonValue>(recurrence[0]);

        Assert.Equal(
            "2026-08-31T11:30:00+09:00",
            startDateTime.GetValue<string>());
        Assert.Equal(
            "Asia/Seoul",
            startTimeZone.GetValue<string>());
        Assert.Equal(
            "RRULE:FREQ=WEEKLY;BYDAY=MO,TH;UNTIL=20261220T145959Z",
            recurrenceRule.GetValue<string>());
    }

    [Fact]
    public void ResourceUsesDateSpecificOffsetsAcrossDaylightSavingTime()
    {
        PlanId planId = new PlanId(
            Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423"));
        GoogleCalendarExportEvent exportEvent = createEvent(
            "course:ITP30003");

        JsonObject resource = GoogleCalendarEventResourceFactory.Create(
            planId,
            new CalendarTimeZoneId("America/New_York"),
            exportEvent);
        JsonObject start = Assert.IsType<JsonObject>(resource["start"]);
        JsonValue startDateTime =
            Assert.IsAssignableFrom<JsonValue>(start["dateTime"]);
        JsonArray recurrence = Assert.IsType<JsonArray>(resource["recurrence"]);
        JsonValue recurrenceRule =
            Assert.IsAssignableFrom<JsonValue>(recurrence[0]);

        Assert.Equal(
            "2026-08-31T11:30:00-04:00",
            startDateTime.GetValue<string>());
        Assert.Equal(
            "RRULE:FREQ=WEEKLY;BYDAY=MO,TH;UNTIL=20261221T045959Z",
            recurrenceRule.GetValue<string>());
    }

    [Fact]
    public void EventIdsAreDeterministicAndGoogleCompatible()
    {
        PlanId planId = new PlanId(
            Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423"));
        GoogleCalendarSourceEventId sourceId =
            new GoogleCalendarSourceEventId("course:ITP30003");

        GoogleCalendarEventId first = GoogleCalendarEventId.Create(planId, sourceId);
        GoogleCalendarEventId second = GoogleCalendarEventId.Create(planId, sourceId);

        Assert.Equal(first, second);
        Assert.Matches("^[a-v0-9]{5,1024}$", first.Value);
    }

    [Fact]
    public void ExportPlanRejectsDuplicateSourceEvents()
    {
        GoogleCalendarExportEvent first = createEvent("same");
        GoogleCalendarExportEvent second = createEvent("same");

        Assert.Throws<ArgumentException>(
            delegate
            {
                new GoogleCalendarExportPlan(
                    PlanId.CreateNew(),
                    new PlanName("2026-2학기 시간표"),
                    new CalendarTimeZoneId("Asia/Seoul"),
                    new GoogleCalendarExportEvent[] { first, second });
            });
    }

    [Fact]
    public void CommonCalendarDocumentMapsWithoutLosingDaysOrOffset()
    {
        RecurringCalendarEvent recurringEvent = new RecurringCalendarEvent(
            new CalendarEventUid("event@timetable-generator.local"),
            new CalendarEventContent("컴퓨터 구조(01)", "OH 401", "담당: 이원형"),
            new DailyTimeRange(
                new ScheduleTime(11, 30),
                new ScheduleTime(12, 15)),
            new EDay[] { EDay.Thursday, EDay.Monday });
        AcademicTermCalendarMetadata metadata = new AcademicTermCalendarMetadata(
            AcademicTerm.Parse("2026-2"),
            new AcademicTermDateRange(
                new DateOnly(2026, 8, 31),
                new DateOnly(2026, 12, 20)),
            new CalendarTimeZoneId("Asia/Seoul"));
        CalendarExportDocument document = new CalendarExportDocument(
            PlanId.CreateNew(),
            new PlanName("2026-2학기 시간표"),
            metadata,
            new RecurringCalendarEvent[] { recurringEvent });

        GoogleCalendarExportPlan plan = GoogleCalendarExportPlan.CreateFromDocument(
            document);

        Assert.Equal(
            TimeSpan.FromHours(9.0),
            plan.TimeZoneId.FindUtcOffset(
                plan.Events[0].FirstOccurrenceDate,
                plan.Events[0].StartTime).Value);
        Assert.Equal(new EDay[] { EDay.Monday, EDay.Thursday }, plan.Events[0].Days);
        Assert.Equal(new DateOnly(2026, 8, 31), plan.Events[0].FirstOccurrenceDate);
        Assert.Equal(new DateOnly(2026, 12, 20), plan.Events[0].LastOccurrenceDate);
    }

    private static GoogleCalendarExportEvent createEvent(string sourceId)
    {
        return new GoogleCalendarExportEvent(
            new GoogleCalendarSourceEventId(sourceId),
            new CalendarEventContent("컴퓨터 구조(01)", "OH 401", "담당: 이원형"),
            new GoogleCalendarRecurrenceDateRange(
                new DateOnly(2026, 8, 31),
                new DateOnly(2026, 12, 20)),
            new DailyTimeRange(
                new ScheduleTime(11, 30),
                new ScheduleTime(12, 15)),
            new EDay[] { EDay.Thursday, EDay.Monday });
    }
}
