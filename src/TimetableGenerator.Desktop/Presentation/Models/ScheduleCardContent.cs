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

        Title = entry.Name;
        if (includeSection)
        {
            Title = entry.NameWithSection;
        }

        LocationOrNull = null;
        if (entry.HasAssignedLocation)
        {
            LocationOrNull = entry.LocationDisplayText;
        }

        ResponsiblePersonOrNull = null;
        if (entry.HasConfirmedInstructor)
        {
            ResponsiblePersonOrNull = entry.InstructorDisplayText;
        }
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
        LocationOrNull = null;
        if (entry.HasLocation)
        {
            LocationOrNull = entry.LocationDisplayText;
        }

        ResponsiblePersonOrNull = null;
        if (entry.HasInstructor)
        {
            ResponsiblePersonOrNull = entry.InstructorDisplayText;
        }
    }
}
