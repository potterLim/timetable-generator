using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal static class ScheduleCalendarProjector
{
    private const string DESCRIPTION_LINE_SEPARATOR = "\n";

    private enum ECourseSummaryStyle
    {
        NameOnly,
        NameWithSection,
    }

    public static CalendarExportDocument ProjectForGoogleCalendar(
        PlanId planId,
        PlanName planName,
        InstitutionName institutionName,
        ScheduleRecommendation displayedSchedule,
        AcademicTermCalendarMetadata academicCalendar)
    {
        return project(
            planId,
            planName,
            institutionName,
            displayedSchedule,
            academicCalendar,
            ECourseSummaryStyle.NameOnly);
    }

    public static CalendarExportDocument ProjectForAppleCalendar(
        PlanId planId,
        PlanName planName,
        InstitutionName institutionName,
        ScheduleRecommendation displayedSchedule,
        AcademicTermCalendarMetadata academicCalendar)
    {
        return project(
            planId,
            planName,
            institutionName,
            displayedSchedule,
            academicCalendar,
            ECourseSummaryStyle.NameWithSection);
    }

    private static CalendarExportDocument project(
        PlanId planId,
        PlanName planName,
        InstitutionName institutionName,
        ScheduleRecommendation displayedSchedule,
        AcademicTermCalendarMetadata academicCalendar,
        ECourseSummaryStyle courseSummaryStyle)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Schedule calendar projection requires a valid plan ID.",
                nameof(planId));
        }

        if (planName == null)
        {
            throw new ArgumentNullException(nameof(planName));
        }

        if (institutionName == null)
        {
            throw new ArgumentNullException(nameof(institutionName));
        }

        if (displayedSchedule == null)
        {
            throw new ArgumentNullException(nameof(displayedSchedule));
        }

        if (academicCalendar == null)
        {
            throw new ArgumentNullException(nameof(academicCalendar));
        }

        Dictionary<CalendarEventProjectionGroupKey, CalendarEventProjectionGroup> groupsByKey = new Dictionary<CalendarEventProjectionGroupKey, CalendarEventProjectionGroup>();
        foreach (ScheduleEntry scheduleEntry in displayedSchedule.Entries)
        {
            CalendarEventSourceIdentity sourceIdentity = createSourceIdentity(scheduleEntry);
            CalendarEventProjectionGroupKey key = new CalendarEventProjectionGroupKey(sourceIdentity, scheduleEntry.TimeRange);
            CalendarEventContent content = createEventContent(
                scheduleEntry,
                courseSummaryStyle);

            CalendarEventProjectionGroup? existingGroupOrNull;
            bool hasExistingGroup = groupsByKey.TryGetValue(key, out existingGroupOrNull);
            CalendarEventProjectionGroup group;
            if (hasExistingGroup && existingGroupOrNull != null)
            {
                group = existingGroupOrNull;
            }
            else
            {
                group = new CalendarEventProjectionGroup(key, content);
                groupsByKey.Add(key, group);
            }

            group.AddDay(scheduleEntry.Day, content);
        }

        List<CalendarEventProjectionGroup> sortedGroups = new List<CalendarEventProjectionGroup>(groupsByKey.Values);
        sortedGroups.Sort(compareGroups);

        List<RecurringCalendarEvent> events = new List<RecurringCalendarEvent>(sortedGroups.Count);
        foreach (CalendarEventProjectionGroup group in sortedGroups)
        {
            events.Add(group.CreateEvent(planId));
        }

        return new CalendarExportDocument(
            planId,
            planName,
            institutionName,
            academicCalendar,
            events);
    }

    private static CalendarEventSourceIdentity createSourceIdentity(ScheduleEntry entry)
    {
        CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
        if (courseEntryOrNull != null)
        {
            return new CalendarEventSourceIdentity("course:" + courseEntryOrNull.OfferingId.Value);
        }

        PersonalScheduleEntry? personalEntryOrNull = entry as PersonalScheduleEntry;
        if (personalEntryOrNull != null)
        {
            return new CalendarEventSourceIdentity("personal:" + personalEntryOrNull.ScheduleId);
        }

        throw new ArgumentOutOfRangeException(nameof(entry), entry, "Unknown schedule entry type.");
    }

    private static CalendarEventContent createEventContent(
        ScheduleEntry entry,
        ECourseSummaryStyle courseSummaryStyle)
    {
        CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
        if (courseEntryOrNull != null)
        {
            return createCourseEventContent(
                courseEntryOrNull,
                courseSummaryStyle);
        }

        PersonalScheduleEntry? personalEntryOrNull = entry as PersonalScheduleEntry;
        if (personalEntryOrNull != null)
        {
            return createPersonalEventContent(personalEntryOrNull);
        }

        throw new ArgumentOutOfRangeException(nameof(entry), entry, "Unknown schedule entry type.");
    }

    private static CalendarEventContent createCourseEventContent(
        CourseScheduleEntry entry,
        ECourseSummaryStyle courseSummaryStyle)
    {
        string summary = findCourseSummary(entry, courseSummaryStyle);
        string location = entry.HasAssignedLocation ? entry.LocationDisplayText : string.Empty;
        List<string> descriptionLines = new List<string>
        {
            "과목 코드: " + entry.Code,
            "분반: " + entry.SectionCode.Value,
        };
        if (entry.HasConfirmedInstructor)
        {
            descriptionLines.Add("교수: " + entry.InstructorDisplayText);
        }

        return new CalendarEventContent(
            summary,
            location,
            string.Join(DESCRIPTION_LINE_SEPARATOR, descriptionLines));
    }

    private static string findCourseSummary(
        CourseScheduleEntry entry,
        ECourseSummaryStyle courseSummaryStyle)
    {
        switch (courseSummaryStyle)
        {
            case ECourseSummaryStyle.NameOnly:
                return entry.Name;
            case ECourseSummaryStyle.NameWithSection:
                return entry.NameWithSection;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(courseSummaryStyle),
                    courseSummaryStyle,
                    "Unknown course calendar summary style.");
        }
    }

    private static CalendarEventContent createPersonalEventContent(PersonalScheduleEntry entry)
    {
        List<string> descriptionLines = new List<string>();
        if (entry.HasSection)
        {
            descriptionLines.Add("분반: " + entry.SectionDisplayText);
        }

        if (entry.HasInstructor)
        {
            descriptionLines.Add("담당: " + entry.InstructorDisplayText);
        }

        string location = entry.HasLocation ? entry.LocationDisplayText : string.Empty;
        return new CalendarEventContent(
            entry.Title,
            location,
            string.Join(DESCRIPTION_LINE_SEPARATOR, descriptionLines));
    }

    private static int compareGroups(
        CalendarEventProjectionGroup left,
        CalendarEventProjectionGroup right)
    {
        int startComparison = left.Key.TimeRange.Start.CompareTo(right.Key.TimeRange.Start);
        if (startComparison != 0)
        {
            return startComparison;
        }

        int summaryComparison = string.Compare(
            left.Content.Summary,
            right.Content.Summary,
            StringComparison.Ordinal);
        if (summaryComparison != 0)
        {
            return summaryComparison;
        }

        return string.Compare(
            left.Key.SourceIdentity.Value,
            right.Key.SourceIdentity.Value,
            StringComparison.Ordinal);
    }
}
