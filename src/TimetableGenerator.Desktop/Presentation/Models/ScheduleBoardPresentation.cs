using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleBoardPresentation
{
    private readonly IReadOnlyList<ScheduleListGroup> mListGroups;

    public ScheduleRecommendation Schedule { get; }

    public ScheduleBoardLayout Layout { get; }

    public PlanName PlanName { get; }

    public InstitutionName InstitutionName { get; }

    public AcademicTerm AcademicTerm { get; }

    public IReadOnlyList<ScheduleListGroup> ListGroups
    {
        get
        {
            return mListGroups;
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
        mListGroups = ScheduleListProjector.Project(schedule.Entries);
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
}
