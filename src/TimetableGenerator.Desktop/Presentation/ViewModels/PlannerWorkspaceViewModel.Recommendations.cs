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

    private IReadOnlyList<PresentationScheduleRecommendation> mRecommendations;

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

            return mRecommendations[mRecommendationIndex];
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
                && IsRecommendationCalculating == false
                && HasRecommendationCalculationError == false;
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

    public string RecommendationInsight
    {
        get
        {
            if (IsRecommendationCalculating)
            {
                return "가능한 분반 조합을 계산하고 있습니다.";
            }

            if (HasRecommendationCalculationError)
            {
                return "추천 시간표를 계산하지 못했습니다.";
            }

            if (HasUnsatisfiedScheduleConstraints)
            {
                if (ActivePlan.HasPersonalSchedules)
                {
                    return "선택한 과목과 개인 일정을 모두 반영할 수 없습니다. "
                        + "개인 일정만 미리 보여드리니 겹치는 과목이나 일정을 조정해 주세요.";
                }

                return "선택한 과목 조합을 함께 배치할 수 없습니다.";
            }

            if (HasRecommendations == false)
            {
                return "과목 선택을 바꾸면 충돌 없는 조합을 다시 계산합니다.";
            }

            if (HasScheduleEntries == false)
            {
                return "시간 미정 과목은 충돌 자동 검증에서 제외됩니다.";
            }

            HashSet<EDay> scheduledDays = new HashSet<EDay>();
            HashSet<string> courseCodes = new HashSet<string>(StringComparer.Ordinal);
            HashSet<PersonalScheduleId> personalScheduleIds =
                new HashSet<PersonalScheduleId>();
            foreach (ScheduleEntry entry in DisplayedSchedule.Entries)
            {
                scheduledDays.Add(entry.Day);
                CourseScheduleEntry? courseEntryOrNull =
                    entry as CourseScheduleEntry;
                if (courseEntryOrNull != null)
                {
                    courseCodes.Add(courseEntryOrNull.Code);
                }

                PersonalScheduleEntry? personalEntryOrNull =
                    entry as PersonalScheduleEntry;
                if (personalEntryOrNull != null)
                {
                    personalScheduleIds.Add(personalEntryOrNull.ScheduleId);
                }
            }

            int freeWeekdayCount = 5 - scheduledDays.Count;
            string insight = courseCodes.Count + "개 시간표 과목";
            if (personalScheduleIds.Count > 0)
            {
                insight += " · 개인 일정 " + personalScheduleIds.Count + "개";
            }

            return insight + " · 공강 "
                + freeWeekdayCount
                + "일";
        }
    }

    public string EmptyScheduleTitle
    {
        get
        {
            if (ActivePlan.SelectedCourseCount == 0)
            {
                return "첫 과목을 추가해 보세요";
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
                return "왼쪽 과목 목록에서 +를 누르면 가능한 시간표를 자동으로 찾아드려요.";
            }

            if (ActivePlan.CourseChoiceGroups.Count == 0)
            {
                return "시간 미정 과목은 내 계획에 그대로 보관했습니다.";
            }

            return "겹치는 과목을 제거하거나 분반 후보를 조정해 주세요.";
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

        mRecommendations = Array.Empty<PresentationScheduleRecommendation>();
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

        List<PresentationScheduleRecommendation> recommendations =
            new List<PresentationScheduleRecommendation>();
        foreach (ApplicationScheduleRecommendation recommendation
            in result.Recommendations)
        {
            recommendations.Add(ScheduleRecommendationProjector.Project(
                recommendation,
                mCatalogProjection));
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
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Ready;
        mRecommendationCalculationError = string.Empty;
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
    }

    private void showRecommendationFailure(Exception exception)
    {
        mRecommendations = Array.Empty<PresentationScheduleRecommendation>();
        mPersonalSchedulePreview = EMPTY_RECOMMENDATION;
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Failed;
        mRecommendationCalculationError =
            "과목 선택은 그대로 보존했습니다. 잠시 후 다시 계산해 주세요.";
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
        foreach (PresentationScheduleRecommendation recommendation
            in mRecommendations)
        {
            layoutEntries.AddRange(recommendation.Entries);
        }

        return ScheduleBoardLayout.CreateForEntries(layoutEntries);
    }

    private void notifyRecommendationCalculationStateChanged()
    {
        raisePropertyChanged(nameof(IsRecommendationCalculating));
        raisePropertyChanged(nameof(HasRecommendationCalculationError));
        raisePropertyChanged(nameof(RecommendationCalculationError));
        raisePropertyChanged(nameof(HasUnsatisfiedScheduleConstraints));
        raisePropertyChanged(nameof(CanExportSchedule));
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
        raisePropertyChanged(nameof(RecommendationInsight));
        raisePropertyChanged(nameof(EmptyScheduleTitle));
        raisePropertyChanged(nameof(EmptyScheduleMessage));
        mPreviousRecommendationCommand.NotifyCanExecuteChanged();
        mNextRecommendationCommand.NotifyCanExecuteChanged();
    }
}
