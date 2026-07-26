using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;
using ApplicationScheduleRecommendation = TimetableGenerator.Application.Scheduling.ScheduleRecommendation;
using PresentationScheduleRecommendation = TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private const int DISPLAYED_RECOMMENDATION_COUNT = 24;

    private static readonly PresentationScheduleRecommendation EMPTY_RECOMMENDATION = new PresentationScheduleRecommendation(Array.Empty<ScheduleEntry>());

    private readonly IScheduleRecommendationProvider mRecommendationProvider;

    private readonly DelegateCommand mPreviousRecommendationCommand;

    private readonly DelegateCommand mNextRecommendationCommand;

    private readonly DelegateCommand mRetryRecommendationCommand;

    private IReadOnlyList<ScheduleRecommendationViewItem> mRecommendations;

    private PresentationScheduleRecommendation mPersonalSchedulePreview;

    private int mRecommendationIndex;

    private CancellationTokenSource mRecommendationCancellationSource;

    private Task mRecommendationRefreshTask;

    private ERecommendationCalculationState mRecommendationCalculationState;

    private string mRecommendationCalculationError;

    private bool mHasUnsatisfiedScheduleConstraints;

    public PresentationScheduleRecommendation ActiveRecommendation
    {
        get
        {
            if (mRecommendations.Count == 0)
            {
                return EMPTY_RECOMMENDATION;
            }

            return mRecommendations[mRecommendationIndex].Schedule;
        }
    }

    public PresentationScheduleRecommendation DisplayedSchedule
    {
        get
        {
            return mRecommendations.Count == 0 ? mPersonalSchedulePreview : ActiveRecommendation;
        }
    }

    public ScheduleBoardPresentation? DisplayedScheduleBoard
    {
        get
        {
            PlanTabItem? activePlanOrNull = mActivePlanOrNull;
            if (activePlanOrNull == null)
            {
                return null;
            }

            CourseCatalog catalog = mCatalogProjection.Document.Catalog;
            return new ScheduleBoardPresentation(
                DisplayedSchedule,
                createScheduleBoardLayout(),
                activePlanOrNull.Name,
                catalog.InstitutionName,
                catalog.Term);
        }
    }

    internal IReadOnlyList<ScheduleBoardPresentation> PngExportCandidates
    {
        get
        {
            PlanTabItem? activePlanOrNull = mActivePlanOrNull;
            if (activePlanOrNull == null || mRecommendations.Count == 0)
            {
                return Array.Empty<ScheduleBoardPresentation>();
            }

            CourseCatalog catalog = mCatalogProjection.Document.Catalog;
            List<ScheduleBoardPresentation> candidates = new List<ScheduleBoardPresentation>(mRecommendations.Count);
            foreach (ScheduleRecommendationViewItem recommendation
                in mRecommendations)
            {
                if (containsSameScheduledOfferings(candidates, recommendation.Schedule))
                {
                    continue;
                }

                candidates.Add(new ScheduleBoardPresentation(recommendation.Schedule, activePlanOrNull.Name, catalog.InstitutionName, catalog.Term));
            }

            return candidates.AsReadOnly();
        }
    }

    public bool CanExportAllPngCandidates
    {
        get
        {
            return PngExportCandidates.Count > 1;
        }
    }

    public string RecommendationSummary
    {
        get
        {
            if (mRecommendations.Count == 0)
            {
                return "0 / 0";
            }

            return (mRecommendationIndex + 1) + " / " + mRecommendations.Count;
        }
    }

    public bool HasRecommendations
    {
        get
        {
            return mRecommendations.Count > 0;
        }
    }

    public bool HasMultipleRecommendations
    {
        get
        {
            return mRecommendations.Count > 1;
        }
    }

    public bool HasScheduleEntries
    {
        get
        {
            return DisplayedSchedule.Entries.Count > 0;
        }
    }

    public bool HasUnsatisfiedScheduleConstraints
    {
        get
        {
            return mHasUnsatisfiedScheduleConstraints;
        }
    }

    public bool CanExportSchedule
    {
        get
        {
            return HasActivePlan
                && HasScheduleEntries
                && HasUnsatisfiedScheduleConstraints == false;
        }
    }

    public bool IsScheduleEmpty
    {
        get
        {
            return HasActivePlan
                && HasScheduleEntries == false
                && HasUnsatisfiedScheduleConstraints == false
                && IsRecommendationCalculating == false
                && HasRecommendationCalculationError == false;
        }
    }

    public bool IsUnsatisfiedScheduleEmpty
    {
        get
        {
            return HasUnsatisfiedScheduleConstraints
                && HasScheduleEntries == false;
        }
    }

    public bool HasUnsatisfiedPersonalSchedulePreview
    {
        get
        {
            return HasUnsatisfiedScheduleConstraints && HasScheduleEntries;
        }
    }

    public bool IsRecommendationCalculating
    {
        get
        {
            return mRecommendationCalculationState == ERecommendationCalculationState.Calculating;
        }
    }

    public bool HasRecommendationCalculationError
    {
        get
        {
            return mRecommendationCalculationState == ERecommendationCalculationState.Failed;
        }
    }

    public string RecommendationCalculationError
    {
        get
        {
            return mRecommendationCalculationError;
        }
    }

    public string EmptyScheduleTitle
    {
        get
        {
            if (HasActivePlan == false)
            {
                return string.Empty;
            }

            if (ActivePlan.SelectedCourseCount == 0)
            {
                return "과목을 선택해 시간표를 구성해 보세요";
            }

            if (HasRecommendations && HasScheduleEntries == false)
            {
                return "시간이 정해진 과목이 없습니다";
            }

            return "겹치지 않는 시간표 조합을 찾지 못했습니다";
        }
    }

    public string EmptyScheduleMessage
    {
        get
        {
            if (HasActivePlan == false)
            {
                return string.Empty;
            }

            if (ActivePlan.SelectedCourseCount == 0)
            {
                return "과목을 선택하면 가능한 시간표를 자동으로 만듭니다.";
            }

            if (HasRecommendations && HasScheduleEntries == false)
            {
                return "시간 미정 과목은 현재 시간표에 유지됩니다.";
            }

            return "겹치는 과목을 빼거나 분반 선호를 바꾸세요.";
        }
    }

    public ICommand PreviousRecommendationCommand
    {
        get
        {
            return mPreviousRecommendationCommand;
        }
    }

    public ICommand NextRecommendationCommand
    {
        get
        {
            return mNextRecommendationCommand;
        }
    }

    public ICommand RetryRecommendationCommand
    {
        get
        {
            return mRetryRecommendationCommand;
        }
    }

    internal Task RecommendationRefreshTask
    {
        get
        {
            return mRecommendationRefreshTask;
        }
    }

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

    private void requestRecommendationRefresh()
    {
        throwIfDisposed();
        mRecommendationCancellationSource.Cancel();
        mRecommendationCancellationSource.Dispose();
        CancellationTokenSource cancellationSource = new CancellationTokenSource();
        mRecommendationCancellationSource = cancellationSource;

        mRecommendations = Array.Empty<ScheduleRecommendationViewItem>();
        mPersonalSchedulePreview = EMPTY_RECOMMENDATION;
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Ready;
        mRecommendationCalculationError = string.Empty;
        mHasUnsatisfiedScheduleConstraints = false;
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
        PlanTabItem? activePlanOrNull = mActivePlanOrNull;
        if (activePlanOrNull == null)
        {
            mRecommendationRefreshTask = Task.CompletedTask;
            return;
        }

        PlanningPlan planSnapshot = activePlanOrNull.Plan;
        mRecommendationCalculationState = ERecommendationCalculationState.Calculating;
        notifyRecommendationCalculationStateChanged();
        mRecommendationRefreshTask = calculateRecommendationsAsync(planSnapshot, cancellationSource);
    }

    private async Task calculateRecommendationsAsync(
        PlanningPlan planSnapshot,
        CancellationTokenSource cancellationSource)
    {
        try
        {
            ScheduleRecommendationLimit recommendationLimit = new ScheduleRecommendationLimit(DISPLAYED_RECOMMENDATION_COUNT);
            ScheduleRecommendationResult result = await Task.Run(
                delegate
                {
                    return mRecommendationProvider.Generate(
                        planSnapshot,
                        recommendationLimit,
                        cancellationSource.Token);
                },
                cancellationSource.Token).ConfigureAwait(false);

            if (cancellationSource.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(
                delegate
                {
                    if (canApplyRecommendationResult(cancellationSource))
                    {
                        applyRecommendationResult(result, planSnapshot);
                    }
                });
        }
        catch (OperationCanceledException)
            when (cancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(
                delegate
                {
                    if (canApplyRecommendationResult(cancellationSource))
                    {
                        showRecommendationFailure(exception);
                    }
                });
        }
    }

    private bool canApplyRecommendationResult(CancellationTokenSource cancellationSource)
    {
        return mIsDisposed == false
            && cancellationSource.IsCancellationRequested == false
            && ReferenceEquals(
                mRecommendationCancellationSource,
                cancellationSource);
    }

    private void applyRecommendationResult(
        ScheduleRecommendationResult result,
        PlanningPlan planSnapshot)
    {
        if (result.HasValidationError)
        {
            throw new InvalidOperationException(
                "The active plan stopped matching its verified catalog: "
                + result.ValidationError
                + ".");
        }

        if (result.Completion == EScheduleRecommendationCompletion.Canceled)
        {
            throw new InvalidOperationException(
                "Recommendation calculation ended without an active cancellation request.");
        }

        List<ScheduleRecommendationViewItem> recommendations = new List<ScheduleRecommendationViewItem>();
        foreach (ApplicationScheduleRecommendation recommendation
            in result.Recommendations)
        {
            PresentationScheduleRecommendation schedule = ScheduleRecommendationProjector.Project(recommendation, mCatalogProjection);
            ScheduleRecommendationBookmark? bookmarkOrNull = createRecommendationBookmarkOrNull(recommendation, planSnapshot);
            recommendations.Add(new ScheduleRecommendationViewItem(schedule, bookmarkOrNull));
        }

        bool hasSelectedScheduledCourses = planSnapshot.CourseChoiceGroups.Count > 0;
        mHasUnsatisfiedScheduleConstraints = recommendations.Count == 0
            && hasSelectedScheduledCourses;
        if (recommendations.Count == 0 && planSnapshot.PersonalSchedules.Count > 0)
        {
            mPersonalSchedulePreview = ScheduleRecommendationProjector.ProjectPersonalSchedules(planSnapshot.PersonalSchedules);
        }
        else
        {
            mPersonalSchedulePreview = EMPTY_RECOMMENDATION;
        }

        mRecommendations = recommendations.AsReadOnly();
        mRecommendationIndex = findRestoredRecommendationIndex(
            recommendations,
            planSnapshot.LastViewedRecommendationOrNull);
        mRecommendationCalculationState = ERecommendationCalculationState.Ready;
        mRecommendationCalculationError = string.Empty;
        synchronizeLastViewedRecommendation(planSnapshot.Id);
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
    }

    private void showRecommendationFailure(Exception exception)
    {
        mRecommendations = Array.Empty<ScheduleRecommendationViewItem>();
        mPersonalSchedulePreview = EMPTY_RECOMMENDATION;
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Failed;
        mRecommendationCalculationError = "과목 선택은 유지됩니다. 다시 계산해 보세요.";
        System.Diagnostics.Debug.WriteLine(exception);
        mHasUnsatisfiedScheduleConstraints = false;
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
    }

    private ScheduleBoardLayout createScheduleBoardLayout()
    {
        if (mRecommendations.Count == 0)
        {
            return ScheduleBoardLayout.CreateForEntries(mPersonalSchedulePreview.Entries);
        }

        List<ScheduleEntry> layoutEntries = new List<ScheduleEntry>();
        foreach (ScheduleRecommendationViewItem recommendation
            in mRecommendations)
        {
            layoutEntries.AddRange(recommendation.Schedule.Entries);
        }

        ScheduleBoardDayRange sharedDayRange = ScheduleBoardDayRange.CreateForEntries(layoutEntries);
        return ScheduleBoardLayout.CreateForEntries(DisplayedSchedule.Entries, sharedDayRange);
    }

    private static ScheduleRecommendationBookmark? createRecommendationBookmarkOrNull(
        ApplicationScheduleRecommendation recommendation,
        PlanningPlan plan)
    {
        if (plan.CourseChoiceGroups.Count == 0)
        {
            return null;
        }

        List<OfferingId> selectedOfferingIds = new List<OfferingId>(plan.CourseChoiceGroups.Count);
        foreach (ScheduledOffering scheduledOffering
            in recommendation.ScheduledOfferings)
        {
            selectedOfferingIds.Add(scheduledOffering.OfferingId);
        }

        foreach (UnscheduledOfferingSelection selection
            in recommendation.UnscheduledSelections)
        {
            if (containsUnscheduledSelection(plan.UnscheduledOfferingSelections, selection.OfferingId) == false)
            {
                selectedOfferingIds.Add(selection.OfferingId);
            }
        }

        return new ScheduleRecommendationBookmark(selectedOfferingIds);
    }

    private static int findRestoredRecommendationIndex(
        IReadOnlyList<ScheduleRecommendationViewItem> recommendations,
        ScheduleRecommendationBookmark? bookmarkOrNull)
    {
        if (bookmarkOrNull == null)
        {
            return 0;
        }

        for (int recommendationIndex = 0; recommendationIndex < recommendations.Count; ++recommendationIndex)
        {
            ScheduleRecommendationBookmark? candidateBookmarkOrNull = recommendations[recommendationIndex].BookmarkOrNull;
            if (candidateBookmarkOrNull != null
                && bookmarkOrNull.HasSameOfferingIds(
                    candidateBookmarkOrNull.SelectedOfferingIds))
            {
                return recommendationIndex;
            }
        }

        return 0;
    }

    private static bool containsUnscheduledSelection(
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

    private static bool containsSameScheduledOfferings(
        IReadOnlyList<ScheduleBoardPresentation> candidates,
        PresentationScheduleRecommendation recommendation)
    {
        HashSet<OfferingId> scheduledOfferingIds = createScheduledOfferingIds(recommendation);
        foreach (ScheduleBoardPresentation candidate in candidates)
        {
            HashSet<OfferingId> candidateOfferingIds = createScheduledOfferingIds(candidate.Schedule);
            if (candidateOfferingIds.SetEquals(scheduledOfferingIds))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<OfferingId> createScheduledOfferingIds(
        PresentationScheduleRecommendation recommendation)
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

    private static bool haveSameRecommendationBookmarks(
        ScheduleRecommendationBookmark? leftOrNull,
        ScheduleRecommendationBookmark? rightOrNull)
    {
        if (leftOrNull == null || rightOrNull == null)
        {
            return leftOrNull == null && rightOrNull == null;
        }

        return leftOrNull.HasSameOfferingIds(rightOrNull.SelectedOfferingIds);
    }

    private void notifyRecommendationCalculationStateChanged()
    {
        raisePropertyChanged(nameof(IsRecommendationCalculating));
        raisePropertyChanged(nameof(HasRecommendationCalculationError));
        raisePropertyChanged(nameof(RecommendationCalculationError));
        raisePropertyChanged(nameof(HasUnsatisfiedScheduleConstraints));
        raisePropertyChanged(nameof(CanExportSchedule));
        raisePropertyChanged(nameof(IsUnsatisfiedScheduleEmpty));
        raisePropertyChanged(nameof(HasUnsatisfiedPersonalSchedulePreview));
        mRetryRecommendationCommand.NotifyCanExecuteChanged();
    }

    private void notifyRecommendationChanged()
    {
        synchronizeDisplayedTimeNotProvidedSelections();
        raisePropertyChanged(nameof(ActiveRecommendation));
        raisePropertyChanged(nameof(DisplayedSchedule));
        raisePropertyChanged(nameof(DisplayedScheduleBoard));
        raisePropertyChanged(nameof(RecommendationSummary));
        raisePropertyChanged(nameof(HasRecommendations));
        raisePropertyChanged(nameof(HasMultipleRecommendations));
        raisePropertyChanged(nameof(CanExportAllPngCandidates));
        raisePropertyChanged(nameof(HasUnsatisfiedScheduleConstraints));
        raisePropertyChanged(nameof(CanExportSchedule));
        raisePropertyChanged(nameof(HasScheduleEntries));
        raisePropertyChanged(nameof(IsScheduleEmpty));
        raisePropertyChanged(nameof(IsUnsatisfiedScheduleEmpty));
        raisePropertyChanged(nameof(HasUnsatisfiedPersonalSchedulePreview));
        raisePropertyChanged(nameof(EmptyScheduleTitle));
        raisePropertyChanged(nameof(EmptyScheduleMessage));
        mPreviousRecommendationCommand.NotifyCanExecuteChanged();
        mNextRecommendationCommand.NotifyCanExecuteChanged();
    }

    private void synchronizeDisplayedTimeNotProvidedSelections()
    {
        ScheduleRecommendationBookmark? recommendationBookmarkOrNull = null;
        if (mRecommendations.Count > 0)
        {
            recommendationBookmarkOrNull = mRecommendations[mRecommendationIndex].BookmarkOrNull;
        }

        PlanTabItem? activePlanOrNull = mActivePlanOrNull;
        if (activePlanOrNull == null)
        {
            return;
        }

        foreach (PlanCourseChoiceGroupItem group
            in activePlanOrNull.CourseChoiceGroups)
        {
            group.SynchronizeSelectedOfferings(recommendationBookmarkOrNull);
        }
    }
}
