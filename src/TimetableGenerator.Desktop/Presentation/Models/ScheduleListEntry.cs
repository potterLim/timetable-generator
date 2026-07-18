using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleListEntry
{
    public EScheduleListEntryKind Kind { get; }

    public EDay Day { get; }

    public DailyTimeRange TimeRange { get; }

    public string KindDisplayText { get; }

    public string Title { get; }

    public string ScheduleDisplayText { get; }

    public string SectionDisplayText { get; }

    public string LocationDisplayText { get; }

    public string ResponsiblePersonDisplayText { get; }

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

    public string AccessibleName
    {
        get
        {
            List<string> details = new List<string>();
            details.Add(KindDisplayText);
            details.Add(Title);
            details.Add(ScheduleDisplayText);
            if (HasSection)
            {
                details.Add("분반 " + SectionDisplayText);
            }

            if (HasLocation)
            {
                details.Add("장소 " + LocationDisplayText);
            }

            if (HasResponsiblePerson)
            {
                details.Add("담당 " + ResponsiblePersonDisplayText);
            }

            return string.Join(", ", details);
        }
    }

    private ScheduleListEntry(
        EScheduleListEntryKind kind,
        EDay day,
        DailyTimeRange timeRange,
        string kindDisplayText,
        string title,
        string sectionDisplayText,
        string locationDisplayText,
        string responsiblePersonDisplayText)
    {
        Kind = kind;
        Day = day;
        TimeRange = timeRange;
        KindDisplayText = kindDisplayText;
        Title = title;
        ScheduleDisplayText =
            ScheduleBoardDayRange.FindFullDayDisplayName(day)
            + " · "
            + timeRange;
        SectionDisplayText = sectionDisplayText;
        LocationDisplayText = locationDisplayText;
        ResponsiblePersonDisplayText = responsiblePersonDisplayText;
    }

    public static ScheduleListEntry CreateForCourse(CourseScheduleEntry entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        string locationDisplayText = entry.HasAssignedLocation
            ? entry.LocationDisplayText
            : string.Empty;
        string responsiblePersonDisplayText = entry.HasConfirmedInstructor
            ? entry.InstructorDisplayText
            : string.Empty;
        return new ScheduleListEntry(
            EScheduleListEntryKind.Course,
            entry.Day,
            entry.TimeRange,
            "과목",
            entry.Name + "(" + entry.SectionCode.Value + ")",
            string.Empty,
            locationDisplayText,
            responsiblePersonDisplayText);
    }

    public static ScheduleListEntry CreateForPersonalSchedule(
        PersonalScheduleEntry entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        return new ScheduleListEntry(
            EScheduleListEntryKind.PersonalSchedule,
            entry.Day,
            entry.TimeRange,
            "개인 일정",
            entry.Title,
            entry.SectionDisplayText,
            entry.LocationDisplayText,
            entry.InstructorDisplayText);
    }
}
