using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public sealed partial class PlanningWorkspaceEditor
{
    public PlanningWorkspace ActivatePlan(PlanningWorkspace workspace, PlanId planId)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        findPlanIndex(workspace, planId);
        return new PlanningWorkspace(workspace.CatalogBinding, planId, workspace.Plans);
    }

    public PlanningWorkspace AddPlan(PlanningWorkspace workspace, PlanningPlan plan)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        List<PlanningPlan> plans = new List<PlanningPlan>(workspace.Plans);
        plans.Add(plan);
        return new PlanningWorkspace(workspace.CatalogBinding, plan.Id, plans);
    }

    public PlanningWorkspace RenamePlan(PlanningWorkspace workspace, PlanId planId, PlanName name)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        PlanningPlan renamedPlan = new PlanningPlan(
            existingPlan.Id,
            name,
            existingPlan.CatalogBinding,
            existingPlan.Content,
            existingPlan.LastViewedRecommendationOrNull);
        return replacePlan(workspace, renamedPlan);
    }

    public PlanningWorkspace RemovePlan(PlanningWorkspace workspace, PlanId planId)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        int removedPlanIndex = findPlanIndex(workspace, planId);
        List<PlanningPlan> remainingPlans = new List<PlanningPlan>(workspace.Plans);
        remainingPlans.RemoveAt(removedPlanIndex);

        PlanId? activePlanIdOrNull = workspace.ActivePlanIdOrNull;
        if (activePlanIdOrNull.HasValue && activePlanIdOrNull.Value == planId)
        {
            if (remainingPlans.Count == 0)
            {
                activePlanIdOrNull = null;
            }
            else
            {
                int replacementIndex = removedPlanIndex;
                if (replacementIndex >= remainingPlans.Count)
                {
                    replacementIndex = remainingPlans.Count - 1;
                }

                activePlanIdOrNull = remainingPlans[replacementIndex].Id;
            }
        }

        return new PlanningWorkspace(workspace.CatalogBinding, activePlanIdOrNull, remainingPlans);
    }

    public PlanningWorkspace ClearPlanContent(PlanningWorkspace workspace, PlanId planId)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        PlanningPlan clearedPlan = new PlanningPlan(
            existingPlan.Id,
            existingPlan.Name,
            existingPlan.CatalogBinding,
            new PlanningPlanContent(
                Array.Empty<CourseChoiceGroup>(),
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()),
            null);
        return replacePlan(workspace, clearedPlan);
    }

    public PlanningWorkspace AddUnscheduledOfferingSelection(
        PlanningWorkspace workspace,
        PlanId planId,
        UnscheduledOfferingSelection selection)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        List<UnscheduledOfferingSelection> selections = new List<UnscheduledOfferingSelection>(existingPlan.UnscheduledOfferingSelections);
        selections.Add(selection);
        PlanningPlan updatedPlan = new PlanningPlan(
            existingPlan.Id,
            existingPlan.Name,
            existingPlan.CatalogBinding,
            new PlanningPlanContent(
                existingPlan.CourseChoiceGroups,
                selections,
                existingPlan.PersonalSchedules));
        return replacePlan(workspace, updatedPlan);
    }

    public PlanningWorkspace RemoveCourse(PlanningWorkspace workspace, PlanId planId, CourseId courseId)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        List<CourseChoiceGroup> courseChoiceGroups = copyCourseChoiceGroupsExceptCourse(existingPlan, courseId);
        List<UnscheduledOfferingSelection> unscheduledSelections = copyUnscheduledSelectionsExceptCourse(existingPlan, courseId);
        PlanningPlan updatedPlan = new PlanningPlan(
            existingPlan.Id,
            existingPlan.Name,
            existingPlan.CatalogBinding,
            new PlanningPlanContent(
                courseChoiceGroups,
                unscheduledSelections,
                existingPlan.PersonalSchedules));
        return replacePlan(workspace, updatedPlan);
    }

    public PlanningWorkspace AddPersonalSchedule(
        PlanningWorkspace workspace,
        PlanId planId,
        PersonalSchedule personalSchedule)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (personalSchedule == null)
        {
            throw new ArgumentNullException(nameof(personalSchedule));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        List<PersonalSchedule> personalSchedules = new List<PersonalSchedule>(existingPlan.PersonalSchedules);
        personalSchedules.Add(personalSchedule);
        return replacePersonalSchedules(workspace, existingPlan, personalSchedules);
    }

    public PlanningWorkspace UpdatePersonalSchedule(
        PlanningWorkspace workspace,
        PlanId planId,
        PersonalSchedule personalSchedule)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (personalSchedule == null)
        {
            throw new ArgumentNullException(nameof(personalSchedule));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        List<PersonalSchedule> personalSchedules = new List<PersonalSchedule>(existingPlan.PersonalSchedules.Count);
        bool hasReplacement = false;
        foreach (PersonalSchedule existingSchedule in existingPlan.PersonalSchedules)
        {
            if (existingSchedule.Id == personalSchedule.Id)
            {
                personalSchedules.Add(personalSchedule);
                hasReplacement = true;
            }
            else
            {
                personalSchedules.Add(existingSchedule);
            }
        }

        if (hasReplacement == false)
        {
            throw new KeyNotFoundException("The planning plan does not contain the personal schedule.");
        }

        return replacePersonalSchedules(workspace, existingPlan, personalSchedules);
    }

    public PlanningWorkspace RemovePersonalSchedule(
        PlanningWorkspace workspace,
        PlanId planId,
        PersonalScheduleId personalScheduleId)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (personalScheduleId.IsValid == false)
        {
            throw new ArgumentException(
                "Personal schedule removal requires a valid ID.",
                nameof(personalScheduleId));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        List<PersonalSchedule> personalSchedules = new List<PersonalSchedule>();
        bool hasRemovedSchedule = false;
        foreach (PersonalSchedule personalSchedule in existingPlan.PersonalSchedules)
        {
            if (personalSchedule.Id == personalScheduleId)
            {
                hasRemovedSchedule = true;
            }
            else
            {
                personalSchedules.Add(personalSchedule);
            }
        }

        if (hasRemovedSchedule == false)
        {
            throw new KeyNotFoundException("The planning plan does not contain the personal schedule.");
        }

        return replacePersonalSchedules(workspace, existingPlan, personalSchedules);
    }

    private static PlanningPlan findPlan(PlanningWorkspace workspace, PlanId planId)
    {
        int planIndex = findPlanIndex(workspace, planId);
        return workspace.Plans[planIndex];
    }

    private static int findPlanIndex(PlanningWorkspace workspace, PlanId planId)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException("Planning workspace edits require a valid plan ID.", nameof(planId));
        }

        for (int index = 0; index < workspace.Plans.Count; ++index)
        {
            if (workspace.Plans[index].Id == planId)
            {
                return index;
            }
        }

        throw new KeyNotFoundException("The planning workspace does not contain the plan.");
    }

    private static PlanningWorkspace replacePlan(
        PlanningWorkspace workspace,
        PlanningPlan replacementPlan)
    {
        List<PlanningPlan> plans = new List<PlanningPlan>(workspace.Plans.Count);
        foreach (PlanningPlan plan in workspace.Plans)
        {
            if (plan.Id == replacementPlan.Id)
            {
                plans.Add(replacementPlan);
            }
            else
            {
                plans.Add(plan);
            }
        }

        return new PlanningWorkspace(workspace.CatalogBinding, workspace.ActivePlanIdOrNull, plans);
    }

    private static PlanningWorkspace replacePersonalSchedules(
        PlanningWorkspace workspace,
        PlanningPlan existingPlan,
        IEnumerable<PersonalSchedule> personalSchedules)
    {
        PlanningPlanContent content = new PlanningPlanContent(
            existingPlan.CourseChoiceGroups,
            existingPlan.UnscheduledOfferingSelections,
            personalSchedules);
        PlanningPlan updatedPlan = new PlanningPlan(
            existingPlan.Id,
            existingPlan.Name,
            existingPlan.CatalogBinding,
            content);
        return replacePlan(workspace, updatedPlan);
    }

    private static List<UnscheduledOfferingSelection> copyUnscheduledSelectionsExceptCourse(
            PlanningPlan plan,
            CourseId courseId)
    {
        List<UnscheduledOfferingSelection> selections = new List<UnscheduledOfferingSelection>();
        foreach (UnscheduledOfferingSelection selection
            in plan.UnscheduledOfferingSelections)
        {
            if (selection.CourseId != courseId)
            {
                selections.Add(selection);
            }
        }

        return selections;
    }
}
