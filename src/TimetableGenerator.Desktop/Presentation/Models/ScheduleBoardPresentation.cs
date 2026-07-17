using System;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleBoardPresentation
{
    public ScheduleRecommendation Schedule { get; }

    public PlanName PlanName { get; }

    public InstitutionName InstitutionName { get; }

    public AcademicTerm AcademicTerm { get; }

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
    {
        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
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
        PlanName = planName;
        InstitutionName = institutionName;
        AcademicTerm = academicTerm;
    }
}
