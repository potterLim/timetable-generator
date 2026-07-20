using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

internal static class ScheduleRecommendationBookmarkRestorer
{
    public static void IncludeIfValid(
        ScheduleRecommendationGenerationState state,
        PlanningPlan plan)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        ScheduleRecommendationBookmark? bookmarkOrNull =
            plan.LastViewedRecommendationOrNull;
        if (bookmarkOrNull == null)
        {
            return;
        }

        foreach (ScheduleRecommendation recommendation in state.Recommendations)
        {
            if (bookmarkMatchesRecommendation(
                bookmarkOrNull,
                recommendation,
                state.UnscheduledSelections))
            {
                return;
            }
        }

        ScheduleRecommendation? bookmarkedRecommendationOrNull =
            createBookmarkedRecommendationOrNull(state, plan, bookmarkOrNull);
        if (bookmarkedRecommendationOrNull == null)
        {
            return;
        }

        if (state.Recommendations.Count < state.MaximumRecommendationCount.Value)
        {
            state.Recommendations.Add(bookmarkedRecommendationOrNull);
            return;
        }

        int replacementIndex = state.Recommendations.Count - 1;
        state.Recommendations[replacementIndex] = bookmarkedRecommendationOrNull;
    }

    private static ScheduleRecommendation? createBookmarkedRecommendationOrNull(
        ScheduleRecommendationGenerationState state,
        PlanningPlan plan,
        ScheduleRecommendationBookmark bookmark)
    {
        ScheduleSearchNode node = ScheduleSearchNode.CreateRoot();
        foreach (ValidatedCourseChoiceGroup courseChoiceGroup
            in state.CourseChoiceGroups)
        {
            ValidatedOfferingCandidate? matchingCandidateOrNull =
                findBookmarkedCandidateOrNull(courseChoiceGroup, bookmark);
            if (matchingCandidateOrNull == null)
            {
                return null;
            }

            if (matchingCandidateOrNull.IsScheduled
                && ScheduleRecommendationConflictChecker.CanAddOffering(
                    node,
                    matchingCandidateOrNull.GetScheduledOffering(),
                    plan.PersonalSchedules) == false)
            {
                return null;
            }

            node = node.CreateChild(matchingCandidateOrNull);
        }

        return new ScheduleRecommendation(
            node.SelectedOfferings,
            state.CombineUnscheduledSelections(
                node.SelectedUnscheduledSelections),
            plan.PersonalSchedules,
            node.Score);
    }

    private static ValidatedOfferingCandidate? findBookmarkedCandidateOrNull(
        ValidatedCourseChoiceGroup courseChoiceGroup,
        ScheduleRecommendationBookmark bookmark)
    {
        foreach (ValidatedOfferingCandidate offeringCandidate
            in courseChoiceGroup.OfferingCandidates)
        {
            if (bookmark.ContainsOffering(offeringCandidate.OfferingId))
            {
                return offeringCandidate;
            }
        }

        return null;
    }

    private static bool bookmarkMatchesRecommendation(
        ScheduleRecommendationBookmark bookmark,
        ScheduleRecommendation recommendation,
        IReadOnlyList<UnscheduledOfferingSelection> fixedUnscheduledSelections)
    {
        List<OfferingId> offeringIds = new List<OfferingId>(
            recommendation.ScheduledOfferings.Count
                + recommendation.UnscheduledSelections.Count);
        foreach (ScheduledOffering offering in recommendation.ScheduledOfferings)
        {
            offeringIds.Add(offering.OfferingId);
        }

        foreach (UnscheduledOfferingSelection selection
            in recommendation.UnscheduledSelections)
        {
            if (containsOfferingId(
                fixedUnscheduledSelections,
                selection.OfferingId) == false)
            {
                offeringIds.Add(selection.OfferingId);
            }
        }

        return bookmark.HasSameOfferingIds(offeringIds);
    }

    private static bool containsOfferingId(
        IReadOnlyList<UnscheduledOfferingSelection> selections,
        OfferingId offeringId)
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
}
