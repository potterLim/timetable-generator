using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal static class IcsCalendarSerializer
{
    private const int MAXIMUM_CONTENT_LINE_OCTETS = 75;

    private const int CONTINUATION_PREFIX_OCTETS = 1;

    private const string CONTENT_LINE_ENDING = "\r\n";

    public static string Serialize(
        CalendarExportDocument document,
        CalendarExportTimestamp exportTimestamp)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        StringBuilder calendarBuilder = new StringBuilder();
        appendContentLine(calendarBuilder, "BEGIN:VCALENDAR");
        appendContentLine(calendarBuilder, "PRODID:-//Timetable Generator//Calendar Export 1.0//KO");
        appendContentLine(calendarBuilder, "VERSION:2.0");
        appendContentLine(calendarBuilder, "CALSCALE:GREGORIAN");
        appendContentLine(calendarBuilder, "METHOD:PUBLISH");
        appendTextProperty(
            calendarBuilder,
            "X-WR-CALNAME",
            document.CalendarName.Value);
        appendContentLine(
            calendarBuilder,
            "X-WR-TIMEZONE:" + document.AcademicCalendar.TimeZoneId.Value);
        appendTimeZone(calendarBuilder, document.AcademicCalendar);
        foreach (RecurringCalendarEvent calendarEvent in document.Events)
        {
            appendEvent(
                calendarBuilder,
                document.AcademicCalendar,
                calendarEvent,
                exportTimestamp);
        }

        appendContentLine(calendarBuilder, "END:VCALENDAR");
        return calendarBuilder.ToString();
    }

    private static void appendTimeZone(
        StringBuilder calendarBuilder,
        AcademicTermCalendarMetadata academicCalendar)
    {
        appendContentLine(calendarBuilder, "BEGIN:VTIMEZONE");
        appendContentLine(
            calendarBuilder,
            "TZID:" + academicCalendar.TimeZoneId.Value);
        appendContentLine(
            calendarBuilder,
            "X-LIC-LOCATION:" + academicCalendar.TimeZoneId.Value);
        IReadOnlyList<CalendarTimeZoneObservance> observances =
            CalendarTimeZoneObservanceResolver.FindForDateRange(
                academicCalendar.TimeZoneId,
                academicCalendar.DateRange);
        foreach (CalendarTimeZoneObservance observance in observances)
        {
            appendTimeZoneObservance(calendarBuilder, observance);
        }

        appendContentLine(calendarBuilder, "END:VTIMEZONE");
    }

    private static void appendTimeZoneObservance(
        StringBuilder calendarBuilder,
        CalendarTimeZoneObservance observance)
    {
        string componentName = formatObservanceKind(observance.Kind);
        appendContentLine(calendarBuilder, "BEGIN:" + componentName);
        appendContentLine(
            calendarBuilder,
            "DTSTART:" + formatLocalDateTime(observance.LocalStart));
        appendContentLine(
            calendarBuilder,
            "TZOFFSETFROM:" + formatUtcOffset(observance.OffsetFrom));
        appendContentLine(
            calendarBuilder,
            "TZOFFSETTO:" + formatUtcOffset(observance.OffsetTo));
        appendContentLine(calendarBuilder, "END:" + componentName);
    }

    private static void appendEvent(
        StringBuilder calendarBuilder,
        AcademicTermCalendarMetadata academicCalendar,
        RecurringCalendarEvent calendarEvent,
        CalendarExportTimestamp exportTimestamp)
    {
        DateOnly firstOccurrenceDate = findFirstOccurrenceDate(
            academicCalendar,
            calendarEvent.Days);
        string timeZoneParameter = ";TZID=" + academicCalendar.TimeZoneId.Value;

        appendContentLine(calendarBuilder, "BEGIN:VEVENT");
        appendContentLine(calendarBuilder, "UID:" + calendarEvent.Uid.Value);
        appendContentLine(
            calendarBuilder,
            "DTSTAMP:" + formatUtcDateTime(exportTimestamp.UtcValue));
        appendTextProperty(
            calendarBuilder,
            "SUMMARY",
            calendarEvent.Content.Summary);
        if (calendarEvent.Content.HasLocation)
        {
            appendTextProperty(
                calendarBuilder,
                "LOCATION",
                calendarEvent.Content.Location);
        }

        if (calendarEvent.Content.HasDescription)
        {
            appendTextProperty(
                calendarBuilder,
                "DESCRIPTION",
                calendarEvent.Content.Description);
        }

        appendContentLine(
            calendarBuilder,
            "DTSTART"
                + timeZoneParameter
                + ":"
                + formatLocalDateTime(
                    firstOccurrenceDate,
                    calendarEvent.TimeRange.Start));
        appendContentLine(
            calendarBuilder,
            "DTEND"
                + timeZoneParameter
                + ":"
                + formatLocalDateTime(
                    firstOccurrenceDate,
                    calendarEvent.TimeRange.End));
        appendContentLine(
            calendarBuilder,
            "RRULE:FREQ=WEEKLY;BYDAY="
                + formatWeekdays(calendarEvent.Days)
                + ";UNTIL="
                + formatUtcDateTime(
                    academicCalendar.GetLastIncludedInstantUtc()));
        appendContentLine(calendarBuilder, "SEQUENCE:0");
        appendContentLine(calendarBuilder, "STATUS:CONFIRMED");
        appendContentLine(calendarBuilder, "TRANSP:OPAQUE");
        appendContentLine(calendarBuilder, "END:VEVENT");
    }

    private static DateOnly findFirstOccurrenceDate(
        AcademicTermCalendarMetadata academicCalendar,
        IReadOnlyList<EDay> days)
    {
        DateOnly firstOccurrenceDate =
            academicCalendar.FindFirstOccurrenceDate(days[0]);
        for (int dayIndex = 1; dayIndex < days.Count; ++dayIndex)
        {
            DateOnly candidateDate =
                academicCalendar.FindFirstOccurrenceDate(days[dayIndex]);
            if (candidateDate < firstOccurrenceDate)
            {
                firstOccurrenceDate = candidateDate;
            }
        }

        return firstOccurrenceDate;
    }

    private static string formatLocalDateTime(
        DateOnly date,
        ScheduleTime time)
    {
        return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
            + "T"
            + time.Hour.ToString("D2", CultureInfo.InvariantCulture)
            + time.Minute.ToString("D2", CultureInfo.InvariantCulture)
            + "00";
    }

    private static string formatLocalDateTime(DateTime localDateTime)
    {
        return localDateTime.ToString(
            "yyyyMMdd'T'HHmmss",
            CultureInfo.InvariantCulture);
    }

    private static string formatUtcDateTime(DateTimeOffset dateTime)
    {
        return dateTime
            .ToUniversalTime()
            .ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
    }

    private static string formatUtcOffset(CalendarUtcOffset utcOffset)
    {
        TimeSpan value = utcOffset.Value;
        char sign = value < TimeSpan.Zero ? '-' : '+';
        TimeSpan absoluteValue = value.Duration();
        int totalHours = (int)absoluteValue.TotalHours;
        return sign
            + totalHours.ToString("D2", CultureInfo.InvariantCulture)
            + absoluteValue.Minutes.ToString("D2", CultureInfo.InvariantCulture);
    }

    private static string formatObservanceKind(
        ECalendarTimeZoneObservanceKind kind)
    {
        switch (kind)
        {
            case ECalendarTimeZoneObservanceKind.Standard:
                return "STANDARD";
            case ECalendarTimeZoneObservanceKind.Daylight:
                return "DAYLIGHT";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Calendar serialization requires a supported time-zone observance kind.");
        }
    }

    private static string formatWeekdays(IReadOnlyList<EDay> days)
    {
        StringBuilder weekdayBuilder = new StringBuilder();
        foreach (EDay day in days)
        {
            if (weekdayBuilder.Length > 0)
            {
                weekdayBuilder.Append(',');
            }

            weekdayBuilder.Append(formatWeekday(day));
        }

        return weekdayBuilder.ToString();
    }

    private static string formatWeekday(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return "MO";
            case EDay.Tuesday:
                return "TU";
            case EDay.Wednesday:
                return "WE";
            case EDay.Thursday:
                return "TH";
            case EDay.Friday:
                return "FR";
            case EDay.Saturday:
                return "SA";
            case EDay.Sunday:
                return "SU";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Calendar recurrence rules require a supported weekday.");
        }
    }

    private static void appendTextProperty(
        StringBuilder calendarBuilder,
        string propertyName,
        string value)
    {
        appendContentLine(
            calendarBuilder,
            propertyName + ":" + escapeText(value));
    }

    private static string escapeText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal);
    }

    private static void appendContentLine(
        StringBuilder calendarBuilder,
        string contentLine)
    {
        int currentLineOctets = 0;
        foreach (Rune rune in contentLine.EnumerateRunes())
        {
            int runeOctets = rune.Utf8SequenceLength;
            if (currentLineOctets > 0
                && currentLineOctets + runeOctets
                    > MAXIMUM_CONTENT_LINE_OCTETS)
            {
                calendarBuilder.Append(CONTENT_LINE_ENDING);
                calendarBuilder.Append(' ');
                currentLineOctets = CONTINUATION_PREFIX_OCTETS;
            }

            calendarBuilder.Append(rune.ToString());
            currentLineOctets += runeOctets;
        }

        calendarBuilder.Append(CONTENT_LINE_ENDING);
    }
}
