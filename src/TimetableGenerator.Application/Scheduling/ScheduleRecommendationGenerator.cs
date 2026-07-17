using System;
using System.Collections.Generic;
using System.Threading;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

public sealed class ScheduleRecommendationGenerator
{
    public ScheduleRecommendationResult GenerateRecommendations(
        ScheduleRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ScheduleRecommendationResult.createCanceled(
                Array.Empty<ScheduleRecommendation>());
        }

        PlanCatalogValidator validator = new PlanCatalogValidator(request.Catalog);
        PlanCatalogValidationResult validationResult = validator.Validate(request.Plan);
        if (validationResult.IsValid == false)
        {
            return ScheduleRecommendationResult.createInvalidPlan(validationResult.Error);
        }

        if (validationResult.CourseChoiceGroups.Count == 0)
        {
            return createResultWithoutScheduledChoices(
                validationResult.UnscheduledSelections,
                request.Plan.PersonalSchedules);
        }

        ScheduleRecommendationGenerationState state =
            new ScheduleRecommendationGenerationState(
                validationResult.CourseChoiceGroups,
                validationResult.UnscheduledSelections,
                request.Plan.PersonalSchedules,
                request.MaximumRecommendationCount,
                cancellationToken);
        generateRecommendations(state);

        if (state.Completion == EScheduleRecommendationCompletion.Canceled)
        {
            return ScheduleRecommendationResult.createCanceled(state.Recommendations);
        }

        ScheduleRecommendationBookmarkRestorer.IncludeIfValid(
            state,
            request.Plan);

        return ScheduleRecommendationResult.createCompleted(
            state.Recommendations,
            state.Completion);
    }

    private static ScheduleRecommendationResult createResultWithoutScheduledChoices(
        IReadOnlyList<UnscheduledOfferingSelection> unscheduledSelections,
        IReadOnlyList<PersonalSchedule> personalSchedules)
    {
        if (unscheduledSelections.Count == 0 && personalSchedules.Count == 0)
        {
            return ScheduleRecommendationResult.createCompleted(
                Array.Empty<ScheduleRecommendation>(),
                EScheduleRecommendationCompletion.Completed);
        }

        ScheduleRecommendation recommendation = new ScheduleRecommendation(
            Array.Empty<ScheduledOffering>(),
            unscheduledSelections,
            personalSchedules,
            RecommendationScore.ZERO);
        return ScheduleRecommendationResult.createCompleted(
            new ScheduleRecommendation[] { recommendation },
            EScheduleRecommendationCompletion.Completed);
    }

    private static void generateRecommendations(
        ScheduleRecommendationGenerationState state)
    {
        while (state.HasPendingNodes() && state.ShouldStop == false)
        {
            if (state.CancellationToken.IsCancellationRequested)
            {
                state.MarkCanceled();
                return;
            }

            ScheduleSearchNode node = state.DequeueNode();
            if (node.NextGroupIndex >= state.CourseChoiceGroups.Count)
            {
                bool hasReachedLimit = addCompletedRecommendation(state, node);
                if (hasReachedLimit)
                {
                    state.MarkMaximumRecommendationCountReached();
                    return;
                }

                continue;
            }

            ValidatedCourseChoiceGroup courseChoiceGroup =
                state.CourseChoiceGroups[node.NextGroupIndex];
            foreach (ValidatedOfferingCandidate offeringCandidate
                in courseChoiceGroup.OfferingCandidates)
            {
                if (state.CancellationToken.IsCancellationRequested)
                {
                    state.MarkCanceled();
                    return;
                }

                if (ScheduleRecommendationConflictChecker.CanAddOffering(
                    node,
                    offeringCandidate.Offering,
                    state.PersonalSchedules) == false)
                {
                    continue;
                }

                ScheduleSearchNode childNode = node.CreateChild(offeringCandidate);
                state.EnqueueNode(childNode);
            }
        }
    }

    private static bool addCompletedRecommendation(
        ScheduleRecommendationGenerationState state,
        ScheduleSearchNode node)
    {
        if (state.Recommendations.Count >= state.MaximumRecommendationCount.Value)
        {
            return true;
        }

        ScheduleRecommendation recommendation = new ScheduleRecommendation(
            node.SelectedOfferings,
            state.UnscheduledSelections,
            state.PersonalSchedules,
            node.Score);
        state.Recommendations.Add(recommendation);
        return false;
    }

}
