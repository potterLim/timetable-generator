using System;
using System.Collections.Generic;
using System.Threading;
using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public sealed class PlanningWorkspaceSession
{
    private readonly CourseCatalog mCatalog;

    private readonly PlanningWorkspaceEditor mEditor;

    private readonly ScheduleRecommendationGenerator mRecommendationGenerator;

    private PlanningWorkspace mWorkspace;

    public CourseCatalog Catalog
    {
        get
        {
            return mCatalog;
        }
    }

    public PlanningWorkspace Workspace
    {
        get
        {
            return mWorkspace;
        }
    }

    public PlanningWorkspaceSession(
        CourseCatalog catalog,
        PlanningWorkspace workspace)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        mCatalog = catalog;
        mEditor = new PlanningWorkspaceEditor();
        mRecommendationGenerator = new ScheduleRecommendationGenerator();
        validateWorkspace(workspace);
        mWorkspace = workspace;
    }

    public PlanningWorkspace ActivatePlan(PlanId planId)
    {
        mWorkspace = mEditor.ActivatePlan(mWorkspace, planId);
        return mWorkspace;
    }

    public PlanningWorkspace AddPlan(PlanId planId, PlanName name)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Planning sessions require a valid new plan ID.",
                nameof(planId));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        PlanCatalogBinding binding = mWorkspace.GetActivePlan().CatalogBinding;
        PlanningPlan plan = new PlanningPlan(
            planId,
            name,
            binding,
            new PlanningPlanContent(
                Array.Empty<CourseChoiceGroup>(),
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        mWorkspace = mEditor.AddPlan(mWorkspace, plan);
        return mWorkspace;
    }

    public PlanningWorkspace RenamePlan(PlanId planId, PlanName name)
    {
        mWorkspace = mEditor.RenamePlan(mWorkspace, planId, name);
        return mWorkspace;
    }

    public PlanningWorkspace RemovePlan(PlanId planId)
    {
        mWorkspace = mEditor.RemovePlan(mWorkspace, planId);
        return mWorkspace;
    }

    public PlanningWorkspace AddCourse(PlanningCourseSelection selection)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        PlanningWorkspace editedWorkspace;
        switch (selection.Kind)
        {
            case EPlanningCourseSelectionKind.ScheduledAlternatives:
                ScheduledCourseChoice choice = new ScheduledCourseChoice(
                    selection.CourseId,
                    selection.GetScheduledOfferingIds());
                editedWorkspace = mEditor.AddScheduledCourseChoice(
                    mWorkspace,
                    mWorkspace.ActivePlanId,
                    choice);
                break;
            case EPlanningCourseSelectionKind.TimeNotProvidedOffering:
                UnscheduledOfferingSelection unscheduledSelection =
                    new UnscheduledOfferingSelection(
                        selection.CourseId,
                        selection.GetTimeNotProvidedOfferingId());
                editedWorkspace = mEditor.AddUnscheduledOfferingSelection(
                    mWorkspace,
                    mWorkspace.ActivePlanId,
                    unscheduledSelection);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(selection),
                    selection.Kind,
                    "Unknown planning course selection kind.");
        }

        validatePlan(editedWorkspace.GetActivePlan());
        mWorkspace = editedWorkspace;
        return mWorkspace;
    }

    public PlanningWorkspace AddCourseChoiceGroup(
        CourseChoiceGroup courseChoiceGroup)
    {
        if (courseChoiceGroup == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroup));
        }

        PlanningWorkspace editedWorkspace = mEditor.AddCourseChoiceGroup(
            mWorkspace,
            mWorkspace.ActivePlanId,
            courseChoiceGroup);
        validatePlan(editedWorkspace.GetActivePlan());
        mWorkspace = editedWorkspace;
        return mWorkspace;
    }

    public PlanningWorkspace UpdateCourseChoiceGroup(
        CourseChoiceGroup courseChoiceGroup)
    {
        if (courseChoiceGroup == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroup));
        }

        PlanningWorkspace editedWorkspace = mEditor.UpdateCourseChoiceGroup(
            mWorkspace,
            mWorkspace.ActivePlanId,
            courseChoiceGroup);
        validatePlan(editedWorkspace.GetActivePlan());
        mWorkspace = editedWorkspace;
        return mWorkspace;
    }

    public PlanningWorkspace RemoveCourseChoiceGroup(
        CourseChoiceGroupId courseChoiceGroupId)
    {
        mWorkspace = mEditor.RemoveCourseChoiceGroup(
            mWorkspace,
            mWorkspace.ActivePlanId,
            courseChoiceGroupId);
        return mWorkspace;
    }

    public PlanningWorkspace RemoveCourse(CourseId courseId)
    {
        mWorkspace = mEditor.RemoveCourse(
            mWorkspace,
            mWorkspace.ActivePlanId,
            courseId);
        return mWorkspace;
    }

    public PlanningWorkspace AddPersonalSchedule(PersonalSchedule personalSchedule)
    {
        if (personalSchedule == null)
        {
            throw new ArgumentNullException(nameof(personalSchedule));
        }

        mWorkspace = mEditor.AddPersonalSchedule(
            mWorkspace,
            mWorkspace.ActivePlanId,
            personalSchedule);
        return mWorkspace;
    }

    public PlanningWorkspace UpdatePersonalSchedule(PersonalSchedule personalSchedule)
    {
        if (personalSchedule == null)
        {
            throw new ArgumentNullException(nameof(personalSchedule));
        }

        mWorkspace = mEditor.UpdatePersonalSchedule(
            mWorkspace,
            mWorkspace.ActivePlanId,
            personalSchedule);
        return mWorkspace;
    }

    public PlanningWorkspace RemovePersonalSchedule(
        PersonalScheduleId personalScheduleId)
    {
        mWorkspace = mEditor.RemovePersonalSchedule(
            mWorkspace,
            mWorkspace.ActivePlanId,
            personalScheduleId);
        return mWorkspace;
    }

    public ScheduleRecommendationResult GenerateRecommendations(
        ScheduleRecommendationLimit recommendationLimit,
        CancellationToken cancellationToken)
    {
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(
            mCatalog,
            mWorkspace.GetActivePlan(),
            recommendationLimit);
        return mRecommendationGenerator.GenerateRecommendations(
            request,
            cancellationToken);
    }

    private void validateWorkspace(PlanningWorkspace workspace)
    {
        PlanCatalogBinding sharedBinding = workspace.Plans[0].CatalogBinding;
        foreach (PlanningPlan plan in workspace.Plans)
        {
            if (plan.CatalogBinding != sharedBinding)
            {
                throw new ArgumentException(
                    "Every session plan must share one catalog artifact binding.",
                    nameof(workspace));
            }

            validatePlan(plan);
        }
    }

    private void validatePlan(PlanningPlan plan)
    {
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(
            mCatalog,
            plan,
            new ScheduleRecommendationLimit(1));
        ScheduleRecommendationResult result =
            mRecommendationGenerator.GenerateRecommendations(
                request,
                CancellationToken.None);
        if (result.HasValidationError)
        {
            throw new ArgumentException(
                "The planning workspace does not match its course catalog: "
                + result.ValidationError
                + ".",
                nameof(plan));
        }
    }
}
