using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private readonly DelegateCommand mCalculateAllRecommendationsCommand;

    private readonly DelegateCommand mCancelAllRecommendationsCommand;

    private CancellationTokenSource? mExhaustiveRecommendationCancellationSourceOrNull;

    private ERecommendationExpansionState mRecommendationExpansionState;

    public bool HasAdditionalRecommendations
    {
        get
        {
            return mRecommendationExpansionState != ERecommendationExpansionState.Unavailable;
        }
    }

    public bool CanCalculateAllRecommendations
    {
        get
        {
            return mRecommendationExpansionState == ERecommendationExpansionState.Available
                || mRecommendationExpansionState == ERecommendationExpansionState.Failed;
        }
    }

    public bool IsCalculatingAllRecommendations
    {
        get
        {
            return mRecommendationExpansionState == ERecommendationExpansionState.Calculating;
        }
    }

    public string AdditionalRecommendationTitle
    {
        get
        {
            return mRecommendationExpansionState switch
            {
                ERecommendationExpansionState.Available => "가능한 시간표가 많습니다",
                ERecommendationExpansionState.Calculating => "모든 가능한 시간표를 계산하고 있습니다",
                ERecommendationExpansionState.Failed => "전체 시간표 계산을 완료하지 못했습니다",
                _ => string.Empty,
            };
        }
    }

    public string AdditionalRecommendationMessage
    {
        get
        {
            return mRecommendationExpansionState switch
            {
                ERecommendationExpansionState.Available => "먼저 " + mRecommendations.Count + "개를 표시합니다. 전체 계산은 시간이 걸릴 수 있습니다.",
                ERecommendationExpansionState.Calculating => "계산 중에도 준비된 시간표를 계속 확인할 수 있습니다.",
                ERecommendationExpansionState.Failed => "먼저 준비한 " + mRecommendations.Count + "개는 계속 확인할 수 있습니다.",
                _ => string.Empty,
            };
        }
    }

    public string CalculateAllRecommendationsActionText
    {
        get
        {
            if (mRecommendationExpansionState == ERecommendationExpansionState.Failed)
            {
                return "다시 계산";
            }

            return "전체 시간표 계산";
        }
    }

    public ICommand CalculateAllRecommendationsCommand
    {
        get
        {
            return mCalculateAllRecommendationsCommand;
        }
    }

    public ICommand CancelAllRecommendationsCommand
    {
        get
        {
            return mCancelAllRecommendationsCommand;
        }
    }

    private bool canCalculateAllRecommendations()
    {
        return CanCalculateAllRecommendations;
    }

    private bool canCancelAllRecommendations()
    {
        return IsCalculatingAllRecommendations
            && mExhaustiveRecommendationCancellationSourceOrNull?.IsCancellationRequested == false;
    }

    private void calculateAllRecommendations()
    {
        throwIfDisposed();
        if (canCalculateAllRecommendations() == false)
        {
            return;
        }

        PlanTabItem? activePlanOrNull = mActivePlanOrNull;
        if (activePlanOrNull == null)
        {
            return;
        }

        CancellationTokenSource cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(mRecommendationCancellationSource.Token);
        mExhaustiveRecommendationCancellationSourceOrNull = cancellationSource;
        mRecommendationExpansionState = ERecommendationExpansionState.Calculating;
        notifyRecommendationExpansionStateChanged();
        mRecommendationRefreshTask = calculateAllRecommendationsAsync(activePlanOrNull.Plan, mRecommendationCancellationSource, cancellationSource);
    }

    private void cancelAllRecommendations()
    {
        if (canCancelAllRecommendations() == false)
        {
            return;
        }

        mExhaustiveRecommendationCancellationSourceOrNull?.Cancel();
        mCancelAllRecommendationsCommand.NotifyCanExecuteChanged();
    }

    private async Task<RecommendationProjectionBatch?> tryProjectAllRecommendationsAutomaticallyAsync(
        PlanningPlan planSnapshot,
        CancellationTokenSource recommendationCancellationSource)
    {
        TimeSpan calculationBudget = mRecommendationCalculationPolicy.AutomaticExhaustiveCalculationBudget;
        if (calculationBudget == TimeSpan.Zero)
        {
            return null;
        }

        using (CancellationTokenSource automaticCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(recommendationCancellationSource.Token))
        {
            automaticCancellationSource.CancelAfter(calculationBudget);
            try
            {
                ScheduleRecommendationResult result = await generateRecommendationsAsync(planSnapshot, ScheduleRecommendationLimit.Unlimited, automaticCancellationSource.Token).ConfigureAwait(false);
                if (result.Completion != EScheduleRecommendationCompletion.Completed)
                {
                    return null;
                }

                return projectRecommendationResult(
                    result,
                    planSnapshot,
                    false,
                    automaticCancellationSource.Token);
            }
            catch (OperationCanceledException)
                when (automaticCancellationSource.IsCancellationRequested
                    && recommendationCancellationSource.IsCancellationRequested == false)
            {
                return null;
            }
            catch (Exception exception)
                when (recommendationCancellationSource.IsCancellationRequested == false)
            {
                System.Diagnostics.Debug.WriteLine(exception);
                return null;
            }
        }
    }

    private async Task calculateAllRecommendationsAsync(
        PlanningPlan planSnapshot,
        CancellationTokenSource recommendationCancellationSource,
        CancellationTokenSource exhaustiveCancellationSource)
    {
        try
        {
            ScheduleRecommendationResult result = await generateRecommendationsAsync(planSnapshot, ScheduleRecommendationLimit.Unlimited, exhaustiveCancellationSource.Token).ConfigureAwait(false);
            if (recommendationCancellationSource.IsCancellationRequested
                || exhaustiveCancellationSource.IsCancellationRequested)
            {
                return;
            }

            RecommendationProjectionBatch projectionBatch = projectRecommendationResult(result, planSnapshot, false, exhaustiveCancellationSource.Token);
            await Dispatcher.UIThread.InvokeAsync(
                delegate
                {
                    if (canApplyExhaustiveRecommendationResult(
                        recommendationCancellationSource,
                        exhaustiveCancellationSource))
                    {
                        PlanningPlan restorationPlan = getCurrentPlanForRestoration(planSnapshot);
                        applyRecommendationProjection(
                            projectionBatch,
                            restorationPlan);
                    }
                });
        }
        catch (OperationCanceledException)
            when (recommendationCancellationSource.IsCancellationRequested
                || exhaustiveCancellationSource.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await Dispatcher.UIThread.InvokeAsync(
                delegate
                {
                    if (canApplyExhaustiveRecommendationResult(
                        recommendationCancellationSource,
                        exhaustiveCancellationSource))
                    {
                        mRecommendationExpansionState = ERecommendationExpansionState.Failed;
                        notifyRecommendationExpansionStateChanged();
                        System.Diagnostics.Debug.WriteLine(exception);
                    }
                });
        }
        finally
        {
            if (mIsDisposed == false)
            {
                await Dispatcher.UIThread.InvokeAsync(
                    delegate
                    {
                        if (mIsDisposed == false
                            && ReferenceEquals(
                                mExhaustiveRecommendationCancellationSourceOrNull,
                                exhaustiveCancellationSource))
                        {
                            mExhaustiveRecommendationCancellationSourceOrNull = null;
                            if (mRecommendationExpansionState == ERecommendationExpansionState.Calculating)
                            {
                                mRecommendationExpansionState = ERecommendationExpansionState.Available;
                            }

                            notifyRecommendationExpansionStateChanged();
                        }
                    });
            }

            exhaustiveCancellationSource.Dispose();
        }
    }

    private bool canApplyExhaustiveRecommendationResult(
        CancellationTokenSource recommendationCancellationSource,
        CancellationTokenSource exhaustiveCancellationSource)
    {
        return canApplyRecommendationResult(recommendationCancellationSource)
            && exhaustiveCancellationSource.IsCancellationRequested == false
            && ReferenceEquals(
                mExhaustiveRecommendationCancellationSourceOrNull,
                exhaustiveCancellationSource);
    }

    private void notifyRecommendationExpansionStateChanged()
    {
        raisePropertyChanged(nameof(RecommendationSummary));
        raisePropertyChanged(nameof(HasAdditionalRecommendations));
        raisePropertyChanged(nameof(CanCalculateAllRecommendations));
        raisePropertyChanged(nameof(IsCalculatingAllRecommendations));
        raisePropertyChanged(nameof(AdditionalRecommendationTitle));
        raisePropertyChanged(nameof(AdditionalRecommendationMessage));
        raisePropertyChanged(nameof(CalculateAllRecommendationsActionText));
        raisePropertyChanged(nameof(CanExportAllPngCandidates));
        mCalculateAllRecommendationsCommand.NotifyCanExecuteChanged();
        mCancelAllRecommendationsCommand.NotifyCanExecuteChanged();
    }
}
