using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleBoardPresentation
{
    private readonly IReadOnlyList<ScheduleListEntry> mListEntries;

    public ScheduleRecommendation Schedule { get; }

    public ScheduleBoardLayout Layout { get; }

    public PlanName PlanName { get; }

    public InstitutionName InstitutionName { get; }

    public AcademicTerm AcademicTerm { get; }

    public IReadOnlyList<ScheduleListEntry> ListEntries
    {
        get
        {
            return mListEntries;
        }
    }

    public string InstitutionTermDisplayText
    {
        get
        {
            return InstitutionName.Value + " · " + AcademicTerm.Id;
        }
    }

    public ScheduleBoardPresentation(
        ScheduleRecommendation schedule,
        PlanName planName,
        InstitutionName institutionName,
        AcademicTerm academicTerm)
        : this(
            schedule,
            createLayout(schedule),
            planName,
            institutionName,
            academicTerm)
    {
    }

    public ScheduleBoardPresentation(
        ScheduleRecommendation schedule,
        ScheduleBoardLayout layout,
        PlanName planName,
        InstitutionName institutionName,
        AcademicTerm academicTerm)
    {
        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
        }

        if (layout == null)
        {
            throw new ArgumentNullException(nameof(layout));
        }

        if (planName == null)
        {
            throw new ArgumentNullException(nameof(planName));
        }

        if (institutionName == null)
        {
            throw new ArgumentNullException(nameof(institutionName));
        }

        if (academicTerm.IsValid == false)
        {
            throw new ArgumentException(
                "Schedule board presentations require a valid academic term.",
                nameof(academicTerm));
        }

        Schedule = schedule;
        Layout = layout;
        PlanName = planName;
        InstitutionName = institutionName;
        AcademicTerm = academicTerm;
        mListEntries = createListEntries(schedule.Entries);
    }

    private static ScheduleBoardLayout createLayout(
        ScheduleRecommendation schedule)
    {
        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
        }

        return ScheduleBoardLayout.CreateForEntries(schedule.Entries);
    }

    private static IReadOnlyList<ScheduleListEntry> createListEntries(
        IReadOnlyList<ScheduleEntry> entries)
    {
        List<ScheduleListEntry> listEntries =
            new List<ScheduleListEntry>(entries.Count);
        foreach (ScheduleEntry entry in entries)
        {
            CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
            if (courseEntryOrNull != null)
            {
                listEntries.Add(
                    ScheduleListEntry.CreateForCourse(courseEntryOrNull));
                continue;
            }

            PersonalScheduleEntry? personalEntryOrNull =
                entry as PersonalScheduleEntry;
            if (personalEntryOrNull != null)
            {
                listEntries.Add(
                    ScheduleListEntry.CreateForPersonalSchedule(
                        personalEntryOrNull));
                continue;
            }

            throw new InvalidOperationException(
                "Schedule lists require a supported schedule entry type.");
        }

        listEntries.Sort(compareListEntries);
        return listEntries.AsReadOnly();
    }

    private static int compareListEntries(
        ScheduleListEntry left,
        ScheduleListEntry right)
    {
        int dayComparison = findDayOrder(left.Day).CompareTo(
            findDayOrder(right.Day));
        if (dayComparison != 0)
        {
            return dayComparison;
        }

        int timeComparison = left.TimeRange.Start.CompareTo(
            right.TimeRange.Start);
        if (timeComparison != 0)
        {
            return timeComparison;
        }

        return string.Compare(left.Title, right.Title, StringComparison.Ordinal);
    }

    private static int findDayOrder(TimetableGenerator.Domain.Scheduling.EDay day)
    {
        switch (day)
        {
            case TimetableGenerator.Domain.Scheduling.EDay.Monday:
                return 1;
            case TimetableGenerator.Domain.Scheduling.EDay.Tuesday:
                return 2;
            case TimetableGenerator.Domain.Scheduling.EDay.Wednesday:
                return 3;
            case TimetableGenerator.Domain.Scheduling.EDay.Thursday:
                return 4;
            case TimetableGenerator.Domain.Scheduling.EDay.Friday:
                return 5;
            case TimetableGenerator.Domain.Scheduling.EDay.Saturday:
                return 6;
            case TimetableGenerator.Domain.Scheduling.EDay.Sunday:
                return 7;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Schedule lists require a defined day of the week.");
        }
    }
}
