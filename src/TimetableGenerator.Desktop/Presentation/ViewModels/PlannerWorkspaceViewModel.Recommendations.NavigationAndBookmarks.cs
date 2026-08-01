using System.Collections.Generic;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;
using ApplicationScheduleRecommendation = TimetableGenerator.Application.Scheduling.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private void selectPreviousRecommendation()
    {
        if (canNavigateRecommendations() == false)
        {
            return;
        }

        --mRecommendationIndex;
        if (mRecommendationIndex < 0)
        {
            mRecommendationIndex = mRecommendations.Count - 1;
        }

        rememberActiveRecommendation();
        notifyRecommendationChanged();
    }

    private void selectNextRecommendation()
    {
        if (canNavigateRecommendations() == false)
        {
            return;
        }

        ++mRecommendationIndex;
        if (mRecommendationIndex >= mRecommendations.Count)
        {
            mRecommendationIndex = 0;
        }

        rememberActiveRecommendation();
        notifyRecommendationChanged();
    }

    private bool canNavigateRecommendations()
    {
        return mRecommendations.Count > 1;
    }

    private bool canRetryRecommendation()
    {
        return HasRecommendationCalculationError;
    }

    private static ScheduleRecommendationBookmark? createRecommendationBookmarkOrNull(ApplicationScheduleRecommendation recommendation, PlanningPlan plan)
    {
        if (plan.CourseChoiceGroups.Count == 0)
        {
            return null;
        }

        List<OfferingId> selectedOfferingIds = new List<OfferingId>(plan.CourseChoiceGroups.Count);
        foreach (ScheduledOffering scheduledOffering in recommendation.ScheduledOfferings)
        {
            selectedOfferingIds.Add(scheduledOffering.OfferingId);
        }

        foreach (UnscheduledOfferingSelection selection in recommendation.UnscheduledSelections)
        {
            if (containsUnscheduledSelection(plan.UnscheduledOfferingSelections, selection.OfferingId) == false)
            {
                selectedOfferingIds.Add(selection.OfferingId);
            }
        }

        return new ScheduleRecommendationBookmark(selectedOfferingIds);
    }

    private static int findRestoredRecommendationIndex(IReadOnlyList<ScheduleRecommendationViewItem> recommendations, ScheduleRecommendationBookmark? bookmarkOrNull)
    {
        if (bookmarkOrNull == null)
        {
            return 0;
        }

        for (int recommendationIndex = 0; recommendationIndex < recommendations.Count; ++recommendationIndex)
        {
            ScheduleRecommendationBookmark? candidateBookmarkOrNull = recommendations[recommendationIndex].BookmarkOrNull;
            if (candidateBookmarkOrNull != null && bookmarkOrNull.HasSameOfferingIds(candidateBookmarkOrNull.SelectedOfferingIds))
            {
                return recommendationIndex;
            }
        }

        return 0;
    }

    private static bool containsUnscheduledSelection(IReadOnlyList<UnscheduledOfferingSelection> selections, OfferingId offeringId)
    {
        foreach (UnscheduledOfferingSelection selection in selections)
        {
            if (selection.OfferingId == offeringId)
            {
                return true;
            }
        }

        return false;
    }

    private void rememberActiveRecommendation()
    {
        ScheduleRecommendationBookmark? bookmarkOrNull = mRecommendations[mRecommendationIndex].BookmarkOrNull;
        updateLastViewedRecommendation(bookmarkOrNull);
    }

    private void synchronizeLastViewedRecommendation(PlanId calculatedPlanId)
    {
        PlanId? activePlanIdOrNull = mSession.Workspace.ActivePlanIdOrNull;
        if (activePlanIdOrNull.HasValue == false || activePlanIdOrNull.Value != calculatedPlanId)
        {
            return;
        }

        ScheduleRecommendationBookmark? bookmarkOrNull = null;
        if (mRecommendations.Count > 0)
        {
            bookmarkOrNull = mRecommendations[mRecommendationIndex].BookmarkOrNull;
        }

        updateLastViewedRecommendation(bookmarkOrNull);
    }

    private void updateLastViewedRecommendation(ScheduleRecommendationBookmark? bookmarkOrNull)
    {
        if (mSession.Workspace.ActivePlanIdOrNull.HasValue == false)
        {
            return;
        }

        PlanningPlan activePlan = mSession.Workspace.GetActivePlan();
        ScheduleRecommendationBookmark? existingBookmarkOrNull = activePlan.LastViewedRecommendationOrNull;
        if (haveSameRecommendationBookmarks(existingBookmarkOrNull, bookmarkOrNull))
        {
            return;
        }

        if (bookmarkOrNull == null)
        {
            mSession.ForgetLastViewedRecommendation();
        }
        else
        {
            mSession.RememberLastViewedRecommendation(bookmarkOrNull);
        }

        mAutosaveQueue.RequestSave(mSession.Workspace);
    }

    private static bool haveSameRecommendationBookmarks(ScheduleRecommendationBookmark? leftOrNull, ScheduleRecommendationBookmark? rightOrNull)
    {
        if (leftOrNull == null || rightOrNull == null)
        {
            return leftOrNull == null && rightOrNull == null;
        }

        return leftOrNull.HasSameOfferingIds(rightOrNull.SelectedOfferingIds);
    }
}
