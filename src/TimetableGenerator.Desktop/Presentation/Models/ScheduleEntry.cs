using System;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleEntry
{
    public string Code { get; }

    public string Name { get; }

    public string InstructorDisplayText { get; }

    public string LocationDisplayText { get; }

    public EDay Day { get; }

    public AcademicPeriod Period { get; }

    public ECourseAccent Accent { get; }

    public ScheduleEntry(
        string code,
        string name,
        string instructorDisplayText,
        string locationDisplayText,
        EDay day,
        AcademicPeriod period,
        ECourseAccent accent)
    {
        ensureSupportedDay(day);
        if (period.IsValid == false)
        {
            throw new ArgumentException("Academic periods must be valid.", nameof(period));
        }

        Code = code;
        Name = name;
        InstructorDisplayText = instructorDisplayText;
        LocationDisplayText = locationDisplayText;
        Day = day;
        Period = period;
        Accent = accent;
    }

    private static void ensureSupportedDay(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
            case EDay.Tuesday:
            case EDay.Wednesday:
            case EDay.Thursday:
            case EDay.Friday:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "The planning workspace supports weekdays only.");
        }
    }
}
