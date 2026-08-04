using System;
using System.Collections.Generic;
using System.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using ApplicationScheduleRecommendation = TimetableGenerator.Application.Scheduling.ScheduleRecommendation;
using PresentationScheduleRecommendation = TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private RecommendationProjectionBatch projectRecommendationResult(
        ScheduleRecommendationResult result,
        PlanningPlan planSnapshot,
        bool hasAdditionalRecommendations,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (result.HasValidationError)
        {
            throw new InvalidOperationException("The active plan stopped matching its verified catalog: " + result.ValidationError + ".");
        }

        if (result.Completion == EScheduleRecommendationCompletion.Canceled)
        {
            throw new InvalidOperationException("Recommendation calculation ended without an active cancellation request.");
        }

        EScheduleRecommendationCompletion expectedCompletion;
        if (hasAdditionalRecommendations)
        {
            expectedCompletion = EScheduleRecommendationCompletion.MaximumRecommendationCountReached;
        }
        else
        {
            expectedCompletion = EScheduleRecommendationCompletion.Completed;
        }
        if (result.Completion != expectedCompletion)
        {
            throw new InvalidOperationException(
                "Recommendation calculation completion did not match the presentation state: "
                + result.Completion
                + ".");
        }

        List<ScheduleRecommendationViewItem> recommendations = new List<ScheduleRecommendationViewItem>();
        foreach (ApplicationScheduleRecommendation recommendation in result.Recommendations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PresentationScheduleRecommendation schedule = ScheduleRecommendationProjector.Project(recommendation, mCatalogProjection);
            ScheduleRecommendationBookmark? bookmarkOrNull = createRecommendationBookmarkOrNull(recommendation, planSnapshot);
            recommendations.Add(new ScheduleRecommendationViewItem(schedule, bookmarkOrNull));
        }

        bool hasSelectedScheduledCourses = planSnapshot.CourseChoiceGroups.Count > 0;
        bool hasUnsatisfiedScheduleConstraints = recommendations.Count == 0 && hasSelectedScheduledCourses;
        PresentationScheduleRecommendation personalSchedulePreview = EMPTY_RECOMMENDATION;
        if (recommendations.Count == 0 && planSnapshot.PersonalSchedules.Count > 0)
        {
            personalSchedulePreview = ScheduleRecommendationProjector.ProjectPersonalSchedules(planSnapshot.PersonalSchedules);
        }

        IReadOnlyList<ScheduleRecommendationViewItem> projectedRecommendations = recommendations.AsReadOnly();
        return new RecommendationProjectionBatch(
            projectedRecommendations,
            createPngExportCandidateSchedules(projectedRecommendations, cancellationToken),
            personalSchedulePreview,
            createRecommendationDayRange(projectedRecommendations, cancellationToken),
            hasUnsatisfiedScheduleConstraints,
            hasAdditionalRecommendations);
    }

    private void applyRecommendationProjection(
        RecommendationProjectionBatch projectionBatch,
        PlanningPlan restorationPlan)
    {
        mRecommendations = projectionBatch.Recommendations;
        mPngExportCandidateSchedules = projectionBatch.PngExportCandidateSchedules;
        mPersonalSchedulePreview = projectionBatch.PersonalSchedulePreview;
        mRecommendationDayRange = projectionBatch.DayRange;
        mHasUnsatisfiedScheduleConstraints = projectionBatch.HasUnsatisfiedScheduleConstraints;
        mRecommendationCalculationState = ERecommendationCalculationState.Ready;
        mRecommendationCalculationError = string.Empty;
        if (projectionBatch.HasAdditionalRecommendations)
        {
            mRecommendationExpansionState = ERecommendationExpansionState.Available;
        }
        else
        {
            mRecommendationExpansionState = ERecommendationExpansionState.Unavailable;
        }
        mRecommendationIndex = findRestoredRecommendationIndex(mRecommendations, restorationPlan.LastViewedRecommendationOrNull);
        synchronizeLastViewedRecommendation(restorationPlan.Id);
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
        notifyRecommendationExpansionStateChanged();
    }

    private ScheduleBoardLayout createScheduleBoardLayout()
    {
        if (mRecommendations.Count == 0)
        {
            return ScheduleBoardLayout.CreateForEntries(mPersonalSchedulePreview.Entries);
        }

        return ScheduleBoardLayout.CreateForEntries(DisplayedSchedule.Entries, mRecommendationDayRange);
    }

    private static ScheduleBoardDayRange createRecommendationDayRange(
        IReadOnlyList<ScheduleRecommendationViewItem> recommendations,
        CancellationToken cancellationToken)
    {
        List<ScheduleEntry> layoutEntries = new List<ScheduleEntry>();
        foreach (ScheduleRecommendationViewItem recommendation in recommendations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            layoutEntries.AddRange(recommendation.Schedule.Entries);
        }

        return ScheduleBoardDayRange.CreateForEntries(layoutEntries);
    }

    private static HashSet<OfferingId> createScheduledOfferingIds(PresentationScheduleRecommendation recommendation)
    {
        HashSet<OfferingId> scheduledOfferingIds = new HashSet<OfferingId>();
        foreach (ScheduleEntry entry in recommendation.Entries)
        {
            CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
            if (courseEntryOrNull != null)
            {
                scheduledOfferingIds.Add(courseEntryOrNull.OfferingId);
            }
        }

        return scheduledOfferingIds;
    }

    private static IReadOnlyList<PresentationScheduleRecommendation> createPngExportCandidateSchedules(
        IReadOnlyList<ScheduleRecommendationViewItem> recommendations,
        CancellationToken cancellationToken)
    {
        List<PresentationScheduleRecommendation> schedules = new List<PresentationScheduleRecommendation>(recommendations.Count);
        HashSet<HashSet<OfferingId>> scheduledOfferingSets = new HashSet<HashSet<OfferingId>>(HashSet<OfferingId>.CreateSetComparer());
        foreach (ScheduleRecommendationViewItem recommendation in recommendations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HashSet<OfferingId> scheduledOfferingIds = createScheduledOfferingIds(recommendation.Schedule);
            if (scheduledOfferingSets.Add(scheduledOfferingIds))
            {
                schedules.Add(recommendation.Schedule);
            }
        }

        return schedules.AsReadOnly();
    }
}
