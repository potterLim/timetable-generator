using System;
using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal static class CalendarTimeZoneObservanceResolver
{
    private const int TIME_ZONE_CONTEXT_YEARS = 1;

    private static readonly TimeSpan TRANSITION_SCAN_INTERVAL = TimeSpan.FromHours(1.0);

    public static IReadOnlyList<CalendarTimeZoneObservance> FindForDateRange(CalendarTimeZoneId timeZoneId, AcademicTermDateRange dateRange)
    {
        if (timeZoneId.IsValid == false)
        {
            throw new ArgumentException("Time-zone observance lookup requires a valid IANA time-zone ID.", nameof(timeZoneId));
        }

        if (dateRange.IsValid == false)
        {
            throw new ArgumentException("Time-zone observance lookup requires a valid date range.", nameof(dateRange));
        }

        TimeZoneInfo timeZone = timeZoneId.findSystemTimeZone();
        int contextStartYear = dateRange.StartDate.Year - TIME_ZONE_CONTEXT_YEARS;
        int contextEndYear = dateRange.EndDate.Year + TIME_ZONE_CONTEXT_YEARS + 1;
        DateTimeOffset scanStartUtc = new DateTimeOffset(contextStartYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset scanEndUtc = new DateTimeOffset(contextEndYear, 1, 1, 0, 0, 0, TimeSpan.Zero);
        CalendarUtcOffset precedingOffset = new CalendarUtcOffset(timeZone.GetUtcOffset(scanStartUtc));
        List<CalendarTimeZoneObservance> observances = new List<CalendarTimeZoneObservance>();
        observances.Add(createBaselineObservance(timeZone, scanStartUtc, precedingOffset));

        DateTimeOffset precedingSampleUtc = scanStartUtc;
        while (precedingSampleUtc < scanEndUtc)
        {
            DateTimeOffset currentSampleUtc = precedingSampleUtc.Add(TRANSITION_SCAN_INTERVAL);
            if (currentSampleUtc > scanEndUtc)
            {
                currentSampleUtc = scanEndUtc;
            }

            CalendarUtcOffset currentOffset = new CalendarUtcOffset(timeZone.GetUtcOffset(currentSampleUtc));
            if (currentOffset != precedingOffset)
            {
                DateTimeOffset transitionUtc = findTransitionUtc(timeZone, precedingSampleUtc, currentSampleUtc, precedingOffset);
                observances.Add(createTransitionObservance(timeZone, transitionUtc, precedingOffset, currentOffset));
                precedingOffset = currentOffset;
            }

            precedingSampleUtc = currentSampleUtc;
        }

        return observances.AsReadOnly();
    }

    private static CalendarTimeZoneObservance createBaselineObservance(TimeZoneInfo timeZone, DateTimeOffset baselineUtc, CalendarUtcOffset utcOffset)
    {
        ECalendarTimeZoneObservanceKind kind = timeZone.IsDaylightSavingTime(baselineUtc) ? ECalendarTimeZoneObservanceKind.Daylight : ECalendarTimeZoneObservanceKind.Standard;
        DateTime localStart = convertToUnspecifiedLocalDateTime(baselineUtc, utcOffset);
        return new CalendarTimeZoneObservance(kind, localStart, utcOffset, utcOffset);
    }

    private static CalendarTimeZoneObservance createTransitionObservance(TimeZoneInfo timeZone, DateTimeOffset transitionUtc, CalendarUtcOffset offsetFrom, CalendarUtcOffset offsetTo)
    {
        ECalendarTimeZoneObservanceKind kind = timeZone.IsDaylightSavingTime(transitionUtc) ? ECalendarTimeZoneObservanceKind.Daylight : ECalendarTimeZoneObservanceKind.Standard;
        DateTime localStart = convertToUnspecifiedLocalDateTime(transitionUtc, offsetFrom);
        return new CalendarTimeZoneObservance(kind, localStart, offsetFrom, offsetTo);
    }

    private static DateTimeOffset findTransitionUtc(TimeZoneInfo timeZone, DateTimeOffset precedingSampleUtc, DateTimeOffset currentSampleUtc, CalendarUtcOffset precedingOffset)
    {
        long precedingUnixSecond = precedingSampleUtc.ToUnixTimeSeconds();
        long currentUnixSecond = currentSampleUtc.ToUnixTimeSeconds();
        while (currentUnixSecond - precedingUnixSecond > 1L)
        {
            long candidateUnixSecond = precedingUnixSecond + ((currentUnixSecond - precedingUnixSecond) / 2L);
            DateTimeOffset candidateUtc = DateTimeOffset.FromUnixTimeSeconds(candidateUnixSecond);
            CalendarUtcOffset candidateOffset = new CalendarUtcOffset(timeZone.GetUtcOffset(candidateUtc));
            if (candidateOffset == precedingOffset)
            {
                precedingUnixSecond = candidateUnixSecond;
            }
            else
            {
                currentUnixSecond = candidateUnixSecond;
            }
        }

        return DateTimeOffset.FromUnixTimeSeconds(currentUnixSecond);
    }

    private static DateTime convertToUnspecifiedLocalDateTime(DateTimeOffset utcDateTime, CalendarUtcOffset utcOffset)
    {
        DateTime offsetLocalDateTime = utcDateTime.UtcDateTime.Add(utcOffset.Value);
        return DateTime.SpecifyKind(offsetLocalDateTime, DateTimeKind.Unspecified);
    }
}
