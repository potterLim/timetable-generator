using System;
using System.Collections.Generic;
using System.Text;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.Calendar;

public sealed class IcsCalendarSerializerTests
{
    [Fact]
    public void CalendarContainsKoreanTimeZoneAndUtcInclusiveTermEnd()
    {
        CalendarExportDocument document = createDocument(
            new RecurringCalendarEvent(
                new CalendarEventUid("course-1@test"),
                new CalendarEventContent(
                    "전자기학(01)",
                    "NTH 311",
                    "과목 코드: ECE20061\n담당: 김민수"),
                new DailyTimeRange(
                    new ScheduleTime(11, 30),
                    new ScheduleTime(12, 45)),
                new EDay[] { EDay.Monday, EDay.Thursday }));

        string serializedCalendar = IcsCalendarSerializer.Serialize(
            document,
            new CalendarExportTimestamp(
                new DateTimeOffset(
                    2026,
                    7,
                    19,
                    12,
                    34,
                    56,
                    TimeSpan.FromHours(9))));

        Assert.Contains("TZID:Asia/Seoul\r\n", serializedCalendar);
        Assert.Contains("TZOFFSETFROM:+0900\r\n", serializedCalendar);
        Assert.Contains("TZOFFSETTO:+0900\r\n", serializedCalendar);
        Assert.Contains("DTSTAMP:20260719T033456Z\r\n", serializedCalendar);
        Assert.Contains(
            "DTSTART;TZID=Asia/Seoul:20260831T113000\r\n",
            serializedCalendar);
        Assert.Contains(
            "DTEND;TZID=Asia/Seoul:20260831T124500\r\n",
            serializedCalendar);
        Assert.Contains(
            "RRULE:FREQ=WEEKLY;BYDAY=MO,TH;UNTIL=20261220T145959Z\r\n",
            serializedCalendar);
    }

    [Fact]
    public void TimeZoneObservancesAndTermEndFollowIanaTransitions()
    {
        RecurringCalendarEvent calendarEvent = new RecurringCalendarEvent(
            new CalendarEventUid("course-1@test"),
            new CalendarEventContent(
                "시간대 검증",
                string.Empty,
                string.Empty),
            new DailyTimeRange(
                new ScheduleTime(11, 30),
                new ScheduleTime(12, 45)),
            new EDay[] { EDay.Monday });
        AcademicTermCalendarMetadata metadata =
            new AcademicTermCalendarMetadata(
                AcademicTerm.Parse("2026-2"),
                new AcademicTermDateRange(
                    new DateOnly(2026, 3, 1),
                    new DateOnly(2026, 12, 20)),
                new CalendarTimeZoneId("America/New_York"));
        CalendarExportDocument document = new CalendarExportDocument(
            new PlanId(
                new Guid("11111111-1111-1111-1111-111111111111")),
            new PlanName("시간대 검증"),
            metadata,
            new RecurringCalendarEvent[] { calendarEvent });

        string serializedCalendar = IcsCalendarSerializer.Serialize(
            document,
            new CalendarExportTimestamp(DateTimeOffset.UnixEpoch));

        Assert.Contains("TZID:America/New_York\r\n", serializedCalendar);
        Assert.Contains(
            "BEGIN:DAYLIGHT\r\n"
                + "DTSTART:20260308T020000\r\n"
                + "TZOFFSETFROM:-0500\r\n"
                + "TZOFFSETTO:-0400\r\n",
            serializedCalendar);
        Assert.Contains(
            "BEGIN:STANDARD\r\n"
                + "DTSTART:20261101T020000\r\n"
                + "TZOFFSETFROM:-0400\r\n"
                + "TZOFFSETTO:-0500\r\n",
            serializedCalendar);
        Assert.Contains(
            "RRULE:FREQ=WEEKLY;BYDAY=MO;UNTIL=20261221T045959Z\r\n",
            serializedCalendar);
    }

    [Fact]
    public void FirstOccurrenceUsesTheEarliestSelectedDayAfterTermStart()
    {
        CalendarExportDocument document = createDocument(
            new RecurringCalendarEvent(
                new CalendarEventUid("event-1@test"),
                new CalendarEventContent("주말 일정", string.Empty, string.Empty),
                new DailyTimeRange(
                    new ScheduleTime(10, 0),
                    new ScheduleTime(11, 0)),
                new EDay[] { EDay.Monday, EDay.Sunday }));

        string serializedCalendar = IcsCalendarSerializer.Serialize(
            document,
            new CalendarExportTimestamp(DateTimeOffset.UnixEpoch));

        Assert.Contains(
            "DTSTART;TZID=Asia/Seoul:20260831T100000\r\n",
            serializedCalendar);
    }

    [Fact]
    public void SundayRecurrenceIncludesTheLastDayOfTheAcademicTerm()
    {
        CalendarExportDocument document = createDocument(
            new RecurringCalendarEvent(
                new CalendarEventUid("sunday-event@test"),
                new CalendarEventContent("일요일 일정", string.Empty, string.Empty),
                new DailyTimeRange(
                    new ScheduleTime(20, 0),
                    new ScheduleTime(21, 0)),
                new EDay[] { EDay.Sunday }));

        string serializedCalendar = IcsCalendarSerializer.Serialize(
            document,
            new CalendarExportTimestamp(DateTimeOffset.UnixEpoch));

        Assert.Contains(
            "DTSTART;TZID=Asia/Seoul:20260906T200000\r\n",
            serializedCalendar);
        Assert.Contains(
            "RRULE:FREQ=WEEKLY;BYDAY=SU;UNTIL=20261220T145959Z\r\n",
            serializedCalendar);
    }

    [Fact]
    public void WeekendRecurrenceKeepsSaturdayAndSundayInCalendarOrder()
    {
        CalendarExportDocument document = createDocument(
            new RecurringCalendarEvent(
                new CalendarEventUid("weekend-event@test"),
                new CalendarEventContent("주말 일정", string.Empty, string.Empty),
                new DailyTimeRange(
                    new ScheduleTime(10, 0),
                    new ScheduleTime(11, 0)),
                new EDay[] { EDay.Sunday, EDay.Saturday }));

        string serializedCalendar = IcsCalendarSerializer.Serialize(
            document,
            new CalendarExportTimestamp(DateTimeOffset.UnixEpoch));

        Assert.Contains(
            "DTSTART;TZID=Asia/Seoul:20260905T100000\r\n",
            serializedCalendar);
        Assert.Contains(
            "RRULE:FREQ=WEEKLY;BYDAY=SA,SU;UNTIL=20261220T145959Z\r\n",
            serializedCalendar);
    }

    [Fact]
    public void TextValuesAreEscapedWithoutOptionalEmptyProperties()
    {
        CalendarExportDocument document = createDocument(
            new RecurringCalendarEvent(
                new CalendarEventUid("event-1@test"),
                new CalendarEventContent(
                    "세미나, 발표; 토론\\준비",
                    string.Empty,
                    "첫째 줄\r\n둘째 줄"),
                new DailyTimeRange(
                    new ScheduleTime(10, 0),
                    new ScheduleTime(11, 0)),
                new EDay[] { EDay.Tuesday }));

        string serializedCalendar = IcsCalendarSerializer.Serialize(
            document,
            new CalendarExportTimestamp(DateTimeOffset.UnixEpoch));

        Assert.Contains(
            "SUMMARY:세미나\\, 발표\\; 토론\\\\준비\r\n",
            serializedCalendar);
        Assert.Contains("DESCRIPTION:첫째 줄\\n둘째 줄\r\n", serializedCalendar);
        Assert.DoesNotContain("\r\nLOCATION:", serializedCalendar);
    }

    [Fact]
    public void UnicodeContentLinesAreFoldedAtSeventyFiveUtf8Octets()
    {
        string longKoreanSummary = new string('가', 80);
        CalendarExportDocument document = createDocument(
            new RecurringCalendarEvent(
                new CalendarEventUid("event-1@test"),
                new CalendarEventContent(
                    longKoreanSummary,
                    string.Empty,
                    string.Empty),
                new DailyTimeRange(
                    new ScheduleTime(10, 0),
                    new ScheduleTime(11, 0)),
                new EDay[] { EDay.Wednesday }));

        string serializedCalendar = IcsCalendarSerializer.Serialize(
            document,
            new CalendarExportTimestamp(DateTimeOffset.UnixEpoch));

        string[] physicalLines = serializedCalendar.Split(
            "\r\n",
            StringSplitOptions.None);
        foreach (string physicalLine in physicalLines)
        {
            int octetCount = Encoding.UTF8.GetByteCount(physicalLine);
            Assert.True(
                octetCount <= 75,
                "RFC 5545 content line exceeded 75 octets: " + octetCount);
        }

        string unfoldedCalendar = serializedCalendar.Replace(
            "\r\n ",
            string.Empty,
            StringComparison.Ordinal);
        Assert.Contains("SUMMARY:" + longKoreanSummary, unfoldedCalendar);
    }

    [Fact]
    public void FourByteUnicodeRunesAreNotSplitWhenContentLinesAreFolded()
    {
        string longEmojiSummary = string.Concat(
            "시간표 ",
            new string('가', 20),
            " 🗓️ ",
            new string('나', 40));
        CalendarExportDocument document = createDocument(
            new RecurringCalendarEvent(
                new CalendarEventUid("event-emoji@test"),
                new CalendarEventContent(
                    longEmojiSummary,
                    string.Empty,
                    string.Empty),
                new DailyTimeRange(
                    new ScheduleTime(10, 0),
                    new ScheduleTime(11, 0)),
                new EDay[] { EDay.Wednesday }));

        string serializedCalendar = IcsCalendarSerializer.Serialize(
            document,
            new CalendarExportTimestamp(DateTimeOffset.UnixEpoch));

        string[] physicalLines = serializedCalendar.Split(
            "\r\n",
            StringSplitOptions.None);
        foreach (string physicalLine in physicalLines)
        {
            Assert.True(Encoding.UTF8.GetByteCount(physicalLine) <= 75);
        }

        string unfoldedCalendar = serializedCalendar.Replace(
            "\r\n ",
            string.Empty,
            StringComparison.Ordinal);
        Assert.Contains("SUMMARY:" + longEmojiSummary, unfoldedCalendar);
        Assert.DoesNotContain('\uFFFD', serializedCalendar);
    }

    [Fact]
    public void SerializationUsesOnlyRfcLineEndingsAndEndsWithCrLf()
    {
        CalendarExportDocument document = createDocument(
            new RecurringCalendarEvent(
                new CalendarEventUid("event-1@test"),
                new CalendarEventContent("일정", string.Empty, string.Empty),
                new DailyTimeRange(
                    new ScheduleTime(10, 0),
                    new ScheduleTime(11, 0)),
                new EDay[] { EDay.Friday }));

        string serializedCalendar = IcsCalendarSerializer.Serialize(
            document,
            new CalendarExportTimestamp(DateTimeOffset.UnixEpoch));

        Assert.EndsWith("END:VCALENDAR\r\n", serializedCalendar);
        string withoutCrLf = serializedCalendar.Replace(
            "\r\n",
            string.Empty,
            StringComparison.Ordinal);
        Assert.DoesNotContain('\r', withoutCrLf);
        Assert.DoesNotContain('\n', withoutCrLf);
    }

    private static CalendarExportDocument createDocument(
        RecurringCalendarEvent calendarEvent)
    {
        return new CalendarExportDocument(
            new PlanId(
                new Guid("11111111-1111-1111-1111-111111111111")),
            new PlanName("2026-2학기 시간표"),
            AcademicTermCalendarMetadataRegistry.findByTerm(
                AcademicTerm.Parse("2026-2"),
                new CalendarTimeZoneId("Asia/Seoul")),
            new List<RecurringCalendarEvent> { calendarEvent });
    }
}
