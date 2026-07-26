using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public sealed partial class PlanningWorkspaceEditor
{
    public PlanningWorkspace AddCourseChoiceGroup(
        PlanningWorkspace workspace,
        PlanId planId,
        CourseChoiceGroup courseChoiceGroup)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (courseChoiceGroup == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroup));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        List<CourseChoiceGroup> courseChoiceGroups = new List<CourseChoiceGroup>(existingPlan.CourseChoiceGroups);
        courseChoiceGroups.Add(courseChoiceGroup);
        return replaceCourseChoiceGroups(workspace, existingPlan, courseChoiceGroups);
    }

    public PlanningWorkspace UpdateCourseChoiceGroup(
        PlanningWorkspace workspace,
        PlanId planId,
        CourseChoiceGroup courseChoiceGroup)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (courseChoiceGroup == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroup));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        List<CourseChoiceGroup> courseChoiceGroups = new List<CourseChoiceGroup>(existingPlan.CourseChoiceGroups.Count);
        bool hasReplacement = false;
        foreach (CourseChoiceGroup existingGroup in existingPlan.CourseChoiceGroups)
        {
            if (existingGroup.Id == courseChoiceGroup.Id)
            {
                courseChoiceGroups.Add(courseChoiceGroup);
                hasReplacement = true;
            }
            else
            {
                courseChoiceGroups.Add(existingGroup);
            }
        }

        if (hasReplacement == false)
        {
            throw new KeyNotFoundException("The planning plan does not contain the course choice group.");
        }

        return replaceCourseChoiceGroups(workspace, existingPlan, courseChoiceGroups);
    }

    public PlanningWorkspace RemoveCourseChoiceGroup(
        PlanningWorkspace workspace,
        PlanId planId,
        CourseChoiceGroupId courseChoiceGroupId)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (courseChoiceGroupId.IsValid == false)
        {
            throw new ArgumentException(
                "Course choice group removal requires a valid ID.",
                nameof(courseChoiceGroupId));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        List<CourseChoiceGroup> courseChoiceGroups = new List<CourseChoiceGroup>();
        bool hasRemovedGroup = false;
        foreach (CourseChoiceGroup courseChoiceGroup
            in existingPlan.CourseChoiceGroups)
        {
            if (courseChoiceGroup.Id == courseChoiceGroupId)
            {
                hasRemovedGroup = true;
            }
            else
            {
                courseChoiceGroups.Add(courseChoiceGroup);
            }
        }

        if (hasRemovedGroup == false)
        {
            throw new KeyNotFoundException("The planning plan does not contain the course choice group.");
        }

        return replaceCourseChoiceGroups(workspace, existingPlan, courseChoiceGroups);
    }

    private static PlanningWorkspace replaceCourseChoiceGroups(
        PlanningWorkspace workspace,
        PlanningPlan existingPlan,
        IEnumerable<CourseChoiceGroup> courseChoiceGroups)
    {
        PlanningPlanContent content = new PlanningPlanContent(
            courseChoiceGroups,
            existingPlan.UnscheduledOfferingSelections,
            existingPlan.PersonalSchedules);
        PlanningPlan updatedPlan = new PlanningPlan(
            existingPlan.Id,
            existingPlan.Name,
            existingPlan.CatalogBinding,
            content);
        return replacePlan(workspace, updatedPlan);
    }

    private static List<CourseChoiceGroup> copyCourseChoiceGroupsExceptCourse(
        PlanningPlan plan,
        CourseId courseId)
    {
        List<CourseChoiceGroup> courseChoiceGroups = new List<CourseChoiceGroup>();
        foreach (CourseChoiceGroup courseChoiceGroup in plan.CourseChoiceGroups)
        {
            List<CourseCandidate> remainingCandidates = new List<CourseCandidate>();
            foreach (CourseCandidate courseCandidate
                in courseChoiceGroup.CourseCandidates)
            {
                if (courseCandidate.CourseId != courseId)
                {
                    remainingCandidates.Add(courseCandidate);
                }
            }

            if (remainingCandidates.Count == 0)
            {
                continue;
            }

            if (remainingCandidates.Count == courseChoiceGroup.CourseCandidates.Count)
            {
                courseChoiceGroups.Add(courseChoiceGroup);
                continue;
            }

            courseChoiceGroups.Add(new CourseChoiceGroup(courseChoiceGroup.Id, courseChoiceGroup.Cardinality, remainingCandidates));
        }

        return courseChoiceGroups;
    }
}
