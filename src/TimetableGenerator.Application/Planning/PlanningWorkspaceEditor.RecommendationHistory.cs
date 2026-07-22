using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public sealed partial class PlanningWorkspaceEditor
{
    public PlanningWorkspace RememberLastViewedRecommendation(
        PlanningWorkspace workspace,
        PlanId planId,
        ScheduleRecommendationBookmark recommendationBookmark)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (recommendationBookmark == null)
        {
            throw new ArgumentNullException(nameof(recommendationBookmark));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        PlanningPlan updatedPlan = new PlanningPlan(
            existingPlan.Id,
            existingPlan.Name,
            existingPlan.CatalogBinding,
            existingPlan.Content,
            recommendationBookmark);
        return replacePlan(workspace, updatedPlan);
    }

    public PlanningWorkspace ForgetLastViewedRecommendation(PlanningWorkspace workspace, PlanId planId)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        PlanningPlan existingPlan = findPlan(workspace, planId);
        if (existingPlan.LastViewedRecommendationOrNull == null)
        {
            return workspace;
        }

        PlanningPlan updatedPlan = new PlanningPlan(
            existingPlan.Id,
            existingPlan.Name,
            existingPlan.CatalogBinding,
            existingPlan.Content);
        return replacePlan(workspace, updatedPlan);
    }
}
