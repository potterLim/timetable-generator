using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleCardContent
{
    public string Title { get; }

    public string? LocationOrNull { get; }

    public string? ResponsiblePersonOrNull { get; }

    public ScheduleCardContent(CourseScheduleEntry entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        Title = entry.NameWithSection;
        LocationOrNull = entry.HasAssignedLocation ? entry.LocationDisplayText : null;
        ResponsiblePersonOrNull = entry.HasConfirmedInstructor ? entry.InstructorDisplayText : null;
    }

    public ScheduleCardContent(PersonalScheduleEntry entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        Title = entry.TitleWithSection;
        LocationOrNull = entry.HasLocation ? entry.LocationDisplayText : null;
        ResponsiblePersonOrNull = entry.HasInstructor ? entry.InstructorDisplayText : null;
    }
}
