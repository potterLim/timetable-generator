namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleEntry
{
    public string Code { get; }

    public string Name { get; }

    public string InstructorDisplayText { get; }

    public string LocationDisplayText { get; }

    public EAcademicDay Day { get; }

    public AcademicPeriod Period { get; }

    public ECourseAccent Accent { get; }

    public ScheduleEntry(
        string code,
        string name,
        string instructorDisplayText,
        string locationDisplayText,
        EAcademicDay day,
        AcademicPeriod period,
        ECourseAccent accent)
    {
        Code = code;
        Name = name;
        InstructorDisplayText = instructorDisplayText;
        LocationDisplayText = locationDisplayText;
        Day = day;
        Period = period;
        Accent = accent;
    }
}
