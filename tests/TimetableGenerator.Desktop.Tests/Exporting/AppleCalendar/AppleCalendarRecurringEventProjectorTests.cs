using System;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class AppleCalendarRecurringEventProjectorTests
{
    [Fact]
    public void MultipleWeekdaysProduceOneRecurringMaster()
    {
        CalendarExportDocument document = createDocument(new CalendarTimeZoneId("Asia/Seoul"));

        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));

        Assert.Equal(new int[] { 2, 5 }, recurringEvent.Weekdays);
        Assert.Equal("전자기학(01)", recurringEvent.Summary);
        Assert.Equal("NTH 311", recurringEvent.Location);
        Assert.Equal("과목 코드: ECE20061", recurringEvent.Notes);
        Assert.Equal("Asia/Seoul", recurringEvent.TimeZoneIdentifier);
        Assert.Equal(document.AcademicCalendar.GetLastIncludedInstantUtc().ToUnixTimeSeconds(), recurringEvent.RecurrenceEndsAtUnixSeconds);
        assertLowercaseSha256(recurringEvent.SourceEventHash);
        assertLowercaseSha256(recurringEvent.Fingerprint);
    }

    [Theory]
    [InlineData("Asia/Seoul")]
    [InlineData("America/New_York")]
    public void ProjectedStartAndEndKeepTheLocalClassTime(string timeZoneIdentifier)
    {
        CalendarExportDocument document = createDocument(new CalendarTimeZoneId(timeZoneIdentifier));

        AppleCalendarRecurringEvent recurringEvent = Assert.Single(AppleCalendarRecurringEventProjector.Project(document));
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneIdentifier);
        DateTimeOffset localStart = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(recurringEvent.StartsAtUnixSeconds), timeZone);
        DateTimeOffset localEnd = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(recurringEvent.EndsAtUnixSeconds), timeZone);

        Assert.Equal(11, localStart.Hour);
        Assert.Equal(30, localStart.Minute);
        Assert.Equal(12, localEnd.Hour);
        Assert.Equal(45, localEnd.Minute);
    }

    private static CalendarExportDocument createDocument(CalendarTimeZoneId timeZoneId)
    {
        RecurringCalendarEvent calendarEvent = new RecurringCalendarEvent(
            new CalendarEventUid("course:ECE20061:01"),
            new CalendarEventContent("전자기학(01)", "NTH 311", "과목 코드: ECE20061"),
            new DailyTimeRange(new ScheduleTime(11, 30), new ScheduleTime(12, 45)),
            new EDay[] { EDay.Monday, EDay.Thursday });
        AcademicTermCalendarMetadata academicCalendar = AcademicTermCalendarMetadataRegistry.findByTerm(AcademicTerm.Parse("2026-2"), timeZoneId);
        return new CalendarExportDocument(
            new PlanId(Guid.Parse("71f3be04-d4c6-41d4-a269-792321e71423")),
            new PlanName("2026-2학기 시간표"),
            new InstitutionName("한동대학교"),
            academicCalendar,
            new RecurringCalendarEvent[] { calendarEvent });
    }

    private static void assertLowercaseSha256(string value)
    {
        Assert.Equal(64, value.Length);
        foreach (char character in value)
        {
            Assert.True(character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
        }
    }
}
