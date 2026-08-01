using System;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Domain.Planning;
using PresentationScheduleRecommendation = TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private void requestRecommendationRefresh()
    {
        throwIfDisposed();
        mExhaustiveRecommendationCancellationSourceOrNull?.Cancel();
        mRecommendationCancellationSource.Cancel();
        mRecommendationCancellationSource.Dispose();
        CancellationTokenSource cancellationSource = new CancellationTokenSource();
        mRecommendationCancellationSource = cancellationSource;

        mRecommendations = Array.Empty<ScheduleRecommendationViewItem>();
        mPngExportCandidateSchedules = Array.Empty<PresentationScheduleRecommendation>();
        mPersonalSchedulePreview = EMPTY_RECOMMENDATION;
        mRecommendationDayRange = ScheduleBoardDayRange.CreateForEntries(EMPTY_RECOMMENDATION.Entries);
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Ready;
        mRecommendationCalculationError = string.Empty;
        mRecommendationExpansionState = ERecommendationExpansionState.Unavailable;
        mHasUnsatisfiedScheduleConstraints = false;
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
        notifyRecommendationExpansionStateChanged();
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

    private async Task calculateRecommendationsAsync(PlanningPlan planSnapshot, CancellationTokenSource cancellationSource)
    {
        try
        {
            ScheduleRecommendationResult initialResult = await generateRecommendationsAsync(planSnapshot, mRecommendationCalculationPolicy.InitialRecommendationLimit, cancellationSource.Token).ConfigureAwait(false);

            if (cancellationSource.IsCancellationRequested)
            {
                return;
            }

            RecommendationProjectionBatch? projectionBatchOrNull = null;
            bool hasAdditionalRecommendations = initialResult.Completion == EScheduleRecommendationCompletion.MaximumRecommendationCountReached;
            if (hasAdditionalRecommendations)
            {
                projectionBatchOrNull = await tryProjectAllRecommendationsAutomaticallyAsync(planSnapshot, cancellationSource).ConfigureAwait(false);
                if (cancellationSource.IsCancellationRequested)
                {
                    return;
                }

                if (projectionBatchOrNull != null)
                {
                    hasAdditionalRecommendations = false;
                }
            }

            RecommendationProjectionBatch projectionBatch;
            if (projectionBatchOrNull == null)
            {
                projectionBatch = projectRecommendationResult(
                    initialResult,
                    planSnapshot,
                    hasAdditionalRecommendations,
                    cancellationSource.Token);
            }
            else
            {
                projectionBatch = projectionBatchOrNull;
            }

            await Dispatcher.UIThread.InvokeAsync(
                delegate
                {
                    if (canApplyRecommendationResult(cancellationSource))
                    {
                        PlanningPlan restorationPlan = getCurrentPlanForRestoration(planSnapshot);
                        applyRecommendationProjection(
                            projectionBatch,
                            restorationPlan);
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

    private Task<ScheduleRecommendationResult> generateRecommendationsAsync(
        PlanningPlan plan,
        ScheduleRecommendationLimit recommendationLimit,
        CancellationToken cancellationToken)
    {
        return Task.Run(
            delegate
            {
                return mRecommendationProvider.Generate(
                    plan,
                    recommendationLimit,
                    cancellationToken);
            },
            cancellationToken);
    }

    private bool canApplyRecommendationResult(CancellationTokenSource cancellationSource)
    {
        return mIsDisposed == false
            && cancellationSource.IsCancellationRequested == false
            && ReferenceEquals(mRecommendationCancellationSource, cancellationSource);
    }

    private PlanningPlan getCurrentPlanForRestoration(PlanningPlan planSnapshot)
    {
        PlanId? activePlanIdOrNull = mSession.Workspace.ActivePlanIdOrNull;
        if (activePlanIdOrNull.HasValue && activePlanIdOrNull.Value == planSnapshot.Id)
        {
            return mSession.Workspace.GetActivePlan();
        }

        return planSnapshot;
    }

    private void showRecommendationFailure(Exception exception)
    {
        mRecommendations = Array.Empty<ScheduleRecommendationViewItem>();
        mPngExportCandidateSchedules = Array.Empty<PresentationScheduleRecommendation>();
        mPersonalSchedulePreview = EMPTY_RECOMMENDATION;
        mRecommendationDayRange = ScheduleBoardDayRange.CreateForEntries(EMPTY_RECOMMENDATION.Entries);
        mRecommendationIndex = 0;
        mRecommendationCalculationState = ERecommendationCalculationState.Failed;
        mRecommendationCalculationError = "과목 선택은 유지됩니다. 다시 계산해 보세요.";
        mRecommendationExpansionState = ERecommendationExpansionState.Unavailable;
        System.Diagnostics.Debug.WriteLine(exception);
        mHasUnsatisfiedScheduleConstraints = false;
        notifyRecommendationChanged();
        notifyRecommendationCalculationStateChanged();
        notifyRecommendationExpansionStateChanged();
    }
}
