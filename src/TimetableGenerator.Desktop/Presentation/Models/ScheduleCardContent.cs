using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleCardContent
{
    public string Title { get; }

    public string? LocationOrNull { get; }

    public string? ResponsiblePersonOrNull { get; }

    public ScheduleCardContent(CourseScheduleEntry entry)
        : this(entry, true)
    {
    }

    private ScheduleCardContent(CourseScheduleEntry entry, bool includeSection)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        Title = includeSection ? entry.NameWithSection : entry.Name;
        LocationOrNull = entry.HasAssignedLocation ? entry.LocationDisplayText : null;
        ResponsiblePersonOrNull = entry.HasConfirmedInstructor ? entry.InstructorDisplayText : null;
    }

    public static ScheduleCardContent CreateForPngExport(CourseScheduleEntry entry)
    {
        return new ScheduleCardContent(entry, false);
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
