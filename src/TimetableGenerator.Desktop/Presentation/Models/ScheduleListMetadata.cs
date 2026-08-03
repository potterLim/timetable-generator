using System;
using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleListMetadata
{
    public string SectionDisplayText { get; }

    public string LocationDisplayText { get; }

    public string ResponsiblePersonDisplayText { get; }

    public string DisplayText { get; }

    public bool HasSection
    {
        get
        {
            return string.IsNullOrWhiteSpace(SectionDisplayText) == false;
        }
    }

    public bool HasLocation
    {
        get
        {
            return string.IsNullOrWhiteSpace(LocationDisplayText) == false;
        }
    }

    public bool HasResponsiblePerson
    {
        get
        {
            return string.IsNullOrWhiteSpace(ResponsiblePersonDisplayText) == false;
        }
    }

    public bool HasDisplayText
    {
        get
        {
            return string.IsNullOrWhiteSpace(DisplayText) == false;
        }
    }

    public ScheduleListMetadata(CourseScheduleEntry courseEntry)
    {
        if (courseEntry == null)
        {
            throw new ArgumentNullException(nameof(courseEntry));
        }

        SectionDisplayText = courseEntry.SectionCode.Value;
        LocationDisplayText = string.Empty;
        if (courseEntry.HasAssignedLocation)
        {
            LocationDisplayText = courseEntry.LocationDisplayText;
        }

        ResponsiblePersonDisplayText = string.Empty;
        if (courseEntry.HasConfirmedInstructor)
        {
            ResponsiblePersonDisplayText = courseEntry.InstructorDisplayText;
        }
        DisplayText = createDisplayTextWithSection(SectionDisplayText, LocationDisplayText, ResponsiblePersonDisplayText);
    }

    public ScheduleListMetadata(PersonalScheduleEntry personalScheduleEntry)
    {
        if (personalScheduleEntry == null)
        {
            throw new ArgumentNullException(nameof(personalScheduleEntry));
        }

        SectionDisplayText = personalScheduleEntry.SectionDisplayText;
        LocationDisplayText = personalScheduleEntry.LocationDisplayText;
        ResponsiblePersonDisplayText = personalScheduleEntry.InstructorDisplayText;
        DisplayText = createDisplayTextWithSection(SectionDisplayText, LocationDisplayText, ResponsiblePersonDisplayText);
    }

    private ScheduleListMetadata(ScheduleListMetadata source, string displayText)
    {
        SectionDisplayText = source.SectionDisplayText;
        LocationDisplayText = source.LocationDisplayText;
        ResponsiblePersonDisplayText = source.ResponsiblePersonDisplayText;
        DisplayText = displayText;
    }

    public bool HasSameContentAs(ScheduleListMetadata? otherOrNull)
    {
        if (otherOrNull == null)
        {
            return false;
        }

        return string.Equals(SectionDisplayText, otherOrNull.SectionDisplayText, StringComparison.Ordinal) && string.Equals(LocationDisplayText, otherOrNull.LocationDisplayText, StringComparison.Ordinal) && string.Equals(ResponsiblePersonDisplayText, otherOrNull.ResponsiblePersonDisplayText, StringComparison.Ordinal);
    }

    public ScheduleListMetadata WithoutSectionInDisplay()
    {
        return new ScheduleListMetadata(this, createDisplayTextWithoutSection(LocationDisplayText, ResponsiblePersonDisplayText));
    }

    private static string createDisplayTextWithSection(string sectionDisplayText, string locationDisplayText, string responsiblePersonDisplayText)
    {
        List<string> visibleValues = new List<string>(3);
        if (string.IsNullOrWhiteSpace(sectionDisplayText) == false)
        {
            visibleValues.Add("(" + sectionDisplayText + ")");
        }

        addVisibleValue(visibleValues, locationDisplayText);
        addVisibleValue(visibleValues, responsiblePersonDisplayText);
        return string.Join(" · ", visibleValues);
    }

    private static string createDisplayTextWithoutSection(string locationDisplayText, string responsiblePersonDisplayText)
    {
        List<string> visibleValues = new List<string>(2);
        addVisibleValue(visibleValues, locationDisplayText);
        addVisibleValue(visibleValues, responsiblePersonDisplayText);
        return string.Join(" · ", visibleValues);
    }

    private static void addVisibleValue(ICollection<string> values, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) == false)
        {
            values.Add(candidate);
        }
    }
}
