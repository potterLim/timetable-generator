using System;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class AcademicTermCalendarMetadata
{
    public AcademicTerm Term { get; }

    public AcademicTermDateRange DateRange { get; }

    public CalendarTimeZoneId TimeZoneId { get; }

    public AcademicTermCalendarMetadata(
        AcademicTerm term,
        AcademicTermDateRange dateRange,
        CalendarTimeZoneId timeZoneId)
    {
        if (term.IsValid == false)
        {
            throw new ArgumentException("Academic calendar metadata requires a valid term.", nameof(term));
        }

        if (dateRange.IsValid == false)
        {
            throw new ArgumentException(
                "Academic calendar metadata requires a valid date range.",
                nameof(dateRange));
        }

        if (timeZoneId.IsValid == false)
        {
            throw new ArgumentException(
                "Academic calendar metadata requires a valid time-zone ID.",
                nameof(timeZoneId));
        }

        Term = term;
        DateRange = dateRange;
        TimeZoneId = timeZoneId;
    }

    public DateOnly FindFirstOccurrenceDate(EDay day)
    {
        DayOfWeek targetDay = convertToDayOfWeek(day);
        int daysUntilTarget = ((int)targetDay - (int)DateRange.StartDate.DayOfWeek + 7) % 7;
        DateOnly firstOccurrenceDate = DateRange.StartDate.AddDays(daysUntilTarget);
        if (firstOccurrenceDate > DateRange.EndDate)
        {
            throw new InvalidOperationException(
                "The academic calendar does not contain the requested weekday.");
        }

        return firstOccurrenceDate;
    }

    public DateTimeOffset GetLastIncludedInstantUtc()
    {
        TimeOnly finalTime = new TimeOnly(23, 59, 59);
        DateTimeOffset finalZonedDateTime = TimeZoneId.ResolveLocalDateTime(DateRange.EndDate, finalTime);
        return finalZonedDateTime.ToUniversalTime();
    }

    private static DayOfWeek convertToDayOfWeek(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return DayOfWeek.Monday;
            case EDay.Tuesday:
                return DayOfWeek.Tuesday;
            case EDay.Wednesday:
                return DayOfWeek.Wednesday;
            case EDay.Thursday:
                return DayOfWeek.Thursday;
            case EDay.Friday:
                return DayOfWeek.Friday;
            case EDay.Saturday:
                return DayOfWeek.Saturday;
            case EDay.Sunday:
                return DayOfWeek.Sunday;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Calendar occurrences require a weekday from Monday through Sunday.");
        }
    }
}
