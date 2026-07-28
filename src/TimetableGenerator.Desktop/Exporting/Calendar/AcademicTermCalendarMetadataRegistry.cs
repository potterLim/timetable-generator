using System;

using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal static class AcademicTermCalendarMetadataRegistry
{
    private static readonly AcademicTerm SECOND_SEMESTER_2026 = AcademicTerm.Parse("2026-2");

    private static readonly AcademicTermDateRange SECOND_SEMESTER_2026_DATE_RANGE = new AcademicTermDateRange(new DateOnly(2026, 8, 31), new DateOnly(2026, 12, 20));

    public static AcademicTermCalendarMetadata FindByTerm(AcademicTerm term)
    {
        AcademicTermDateRange dateRange = findDateRange(term);
        CalendarTimeZoneId localTimeZoneId = CalendarTimeZoneId.CreateFromSystemTimeZone(TimeZoneInfo.Local);
        return new AcademicTermCalendarMetadata(term, dateRange, localTimeZoneId);
    }

    internal static AcademicTermCalendarMetadata findByTerm(AcademicTerm term, CalendarTimeZoneId timeZoneId)
    {
        AcademicTermDateRange dateRange = findDateRange(term);
        return new AcademicTermCalendarMetadata(term, dateRange, timeZoneId);
    }

    private static AcademicTermDateRange findDateRange(AcademicTerm term)
    {
        if (term.IsValid == false)
        {
            throw new ArgumentException("Calendar metadata lookup requires a valid academic term.", nameof(term));
        }

        if (term == SECOND_SEMESTER_2026)
        {
            return SECOND_SEMESTER_2026_DATE_RANGE;
        }

        throw new NotSupportedException("Calendar export is not configured for academic term " + term + ".");
    }
}
