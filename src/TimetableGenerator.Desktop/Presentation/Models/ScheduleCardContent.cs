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

        if (includeSection)
        {
            Title = entry.NameWithSection;
        }
        else
        {
            Title = entry.Name;
        }

        if (entry.HasAssignedLocation)
        {
            LocationOrNull = entry.LocationDisplayText;
        }
        else
        {
            LocationOrNull = null;
        }

        if (entry.HasConfirmedInstructor)
        {
            ResponsiblePersonOrNull = entry.InstructorDisplayText;
        }
        else
        {
            ResponsiblePersonOrNull = null;
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
        if (entry.HasLocation)
        {
            LocationOrNull = entry.LocationDisplayText;
        }
        else
        {
            LocationOrNull = null;
        }

        if (entry.HasInstructor)
        {
            ResponsiblePersonOrNull = entry.InstructorDisplayText;
        }
        else
        {
            ResponsiblePersonOrNull = null;
        }
    }
}
