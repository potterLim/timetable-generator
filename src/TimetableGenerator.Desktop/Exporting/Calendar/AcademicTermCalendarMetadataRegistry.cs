using System;

using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal static class AcademicTermCalendarMetadataRegistry
{
    private static readonly AcademicTerm SECOND_SEMESTER_2026 =
        AcademicTerm.Parse("2026-2");

    private static readonly AcademicTermCalendarMetadata SECOND_SEMESTER_2026_METADATA =
        new AcademicTermCalendarMetadata(
            SECOND_SEMESTER_2026,
            new AcademicTermDateRange(
                new DateOnly(2026, 8, 31),
                new DateOnly(2026, 12, 20)),
            new CalendarTimeZoneId("Asia/Seoul"));

    public static AcademicTermCalendarMetadata FindByTerm(AcademicTerm term)
    {
        if (term.IsValid == false)
        {
            throw new ArgumentException(
                "Calendar metadata lookup requires a valid academic term.",
                nameof(term));
        }

        if (term == SECOND_SEMESTER_2026)
        {
            return SECOND_SEMESTER_2026_METADATA;
        }

        throw new NotSupportedException(
            "Calendar export is not configured for academic term " + term + ".");
    }
}
