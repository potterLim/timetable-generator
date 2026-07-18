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
using ApplicationScheduleRecommendation =
    TimetableGenerator.Application.Scheduling.ScheduleRecommendation;
using PresentationScheduleRecommendation =
    TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private const int DISPLAYED_RECOMMENDATION_COUNT = 24;

    private static readonly PresentationScheduleRecommendation EMPTY_RECOMMENDATION =
        new PresentationScheduleRecommendation(Array.Empty<ScheduleEntry>());

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
            return mRecommendations.Count == 0
                ? mPersonalSchedulePreview
                : ActiveRecommendation;
        }
    }

    public ScheduleBoardPresentation DisplayedScheduleBoard
    {
        get
        {
            CourseCatalog catalog = mCatalogProjection.Document.Catalog;
            return new ScheduleBoardPresentation(
                DisplayedSchedule,
                createScheduleBoardLayout(),
                ActivePlan.Name,
                catalog.InstitutionName,
                catalog.Term);
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
            return HasScheduleEntries && HasUnsatisfiedScheduleConstraints == false;
        }
    }

    public bool IsScheduleEmpty
    {
        get
        {
            return HasScheduleEntries == false
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
            return mRecommendationCalculationState
                == ERecommendationCalculationState.Calculating;
        }
    }

    public bool HasRecommendationCalculationError
    {
        get
        {
            return mRecommendationCalculationState
                == ERecommendationCalculationState.Failed;
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
            if (ActivePlan.SelectedCourseCount == 0)
            {
                return "과목을 추가해 시작하세요";
            }

            if (ActivePlan.CourseChoiceGroups.Count == 0)
            {
                return "시간표가 있는 과목이 없습니다";
            }

            return "겹치지 않는 시간표를 찾지 못했습니다";
        }
    }

    public string EmptyScheduleMessage
    {
        get
        {
            if (ActivePlan.SelectedCourseCount == 0)
            {
                return "과목을 추가하면 가능한 시간표를 자동으로 만듭니다.";
            }

            if (ActivePlan.CourseChoiceGroups.Count == 0)
            {
                return "시간 미정 과목은 내 계획에 보관됩니다.";
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
        PlanningPlan planSnapshot = mSession.Workspace.GetActivePlan();

        mRecommendations = Array.Empty<ScheduleRecommendationViewItem>();
        mPersonalSchedulePreview = EMPTY_RECOMMENDATION;
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Calculating;
        mRecommendationCalculationError = string.Empty;
        mHasUnsatisfiedScheduleConstraints = false;
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
        mRecommendationRefreshTask = calculateRecommendationsAsync(
            planSnapshot,
            cancellationSource);
    }

    private async Task calculateRecommendationsAsync(
        PlanningPlan planSnapshot,
        CancellationTokenSource cancellationSource)
    {
        try
        {
            ScheduleRecommendationLimit recommendationLimit =
                new ScheduleRecommendationLimit(DISPLAYED_RECOMMENDATION_COUNT);
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

    private bool canApplyRecommendationResult(
        CancellationTokenSource cancellationSource)
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

        List<ScheduleRecommendationViewItem> recommendations =
            new List<ScheduleRecommendationViewItem>();
        foreach (ApplicationScheduleRecommendation recommendation
            in result.Recommendations)
        {
            PresentationScheduleRecommendation schedule =
                ScheduleRecommendationProjector.Project(
                    recommendation,
                    mCatalogProjection);
            ScheduleRecommendationBookmark? bookmarkOrNull =
                createRecommendationBookmarkOrNull(recommendation);
            recommendations.Add(new ScheduleRecommendationViewItem(
                schedule,
                bookmarkOrNull));
        }

        bool hasSelectedScheduledCourses =
            planSnapshot.CourseChoiceGroups.Count > 0;
        mHasUnsatisfiedScheduleConstraints = recommendations.Count == 0
            && hasSelectedScheduledCourses;
        if (recommendations.Count == 0
            && planSnapshot.PersonalSchedules.Count > 0)
        {
            mPersonalSchedulePreview =
                ScheduleRecommendationProjector.ProjectPersonalSchedules(
                planSnapshot.PersonalSchedules);
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
        mRecommendationCalculationError =
            "과목 선택은 유지됩니다. 다시 계산해 보세요.";
        System.Diagnostics.Debug.WriteLine(exception);
        mHasUnsatisfiedScheduleConstraints = false;
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
    }

    private ScheduleBoardLayout createScheduleBoardLayout()
    {
        if (mRecommendations.Count == 0)
        {
            return ScheduleBoardLayout.CreateForEntries(
                mPersonalSchedulePreview.Entries);
        }

        List<ScheduleEntry> layoutEntries = new List<ScheduleEntry>();
        foreach (ScheduleRecommendationViewItem recommendation
            in mRecommendations)
        {
            layoutEntries.AddRange(recommendation.Schedule.Entries);
        }

        return ScheduleBoardLayout.CreateForEntries(layoutEntries);
    }

    private static ScheduleRecommendationBookmark? createRecommendationBookmarkOrNull(
        ApplicationScheduleRecommendation recommendation)
    {
        if (recommendation.ScheduledOfferings.Count == 0)
        {
            return null;
        }

        List<OfferingId> scheduledOfferingIds = new List<OfferingId>(
            recommendation.ScheduledOfferings.Count);
        foreach (ScheduledOffering scheduledOffering
            in recommendation.ScheduledOfferings)
        {
            scheduledOfferingIds.Add(scheduledOffering.OfferingId);
        }

        return new ScheduleRecommendationBookmark(scheduledOfferingIds);
    }

    private static int findRestoredRecommendationIndex(
        IReadOnlyList<ScheduleRecommendationViewItem> recommendations,
        ScheduleRecommendationBookmark? bookmarkOrNull)
    {
        if (bookmarkOrNull == null)
        {
            return 0;
        }

        for (int recommendationIndex = 0;
            recommendationIndex < recommendations.Count;
            ++recommendationIndex)
        {
            ScheduleRecommendationBookmark? candidateBookmarkOrNull =
                recommendations[recommendationIndex].BookmarkOrNull;
            if (candidateBookmarkOrNull != null
                && bookmarkOrNull.HasSameScheduledOfferingIds(
                    candidateBookmarkOrNull.ScheduledOfferingIds))
            {
                return recommendationIndex;
            }
        }

        return 0;
    }

    private void rememberActiveRecommendation()
    {
        ScheduleRecommendationBookmark? bookmarkOrNull =
            mRecommendations[mRecommendationIndex].BookmarkOrNull;
        updateLastViewedRecommendation(bookmarkOrNull);
    }

    private void synchronizeLastViewedRecommendation(PlanId calculatedPlanId)
    {
        if (mSession.Workspace.ActivePlanId != calculatedPlanId)
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

    private void updateLastViewedRecommendation(
        ScheduleRecommendationBookmark? bookmarkOrNull)
    {
        PlanningPlan activePlan = mSession.Workspace.GetActivePlan();
        ScheduleRecommendationBookmark? existingBookmarkOrNull =
            activePlan.LastViewedRecommendationOrNull;
        if (haveSameRecommendationBookmarks(
            existingBookmarkOrNull,
            bookmarkOrNull))
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

        return leftOrNull.HasSameScheduledOfferingIds(
            rightOrNull.ScheduledOfferingIds);
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
        raisePropertyChanged(nameof(ActiveRecommendation));
        raisePropertyChanged(nameof(DisplayedSchedule));
        raisePropertyChanged(nameof(DisplayedScheduleBoard));
        raisePropertyChanged(nameof(RecommendationSummary));
        raisePropertyChanged(nameof(HasRecommendations));
        raisePropertyChanged(nameof(HasMultipleRecommendations));
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
}
