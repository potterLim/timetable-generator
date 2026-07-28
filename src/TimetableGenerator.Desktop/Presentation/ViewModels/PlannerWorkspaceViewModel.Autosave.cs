using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Threading;

using TimetableGenerator.Desktop.Storage;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private static readonly TimeSpan AUTOSAVE_SAVING_INDICATOR_DELAY = TimeSpan.FromMilliseconds(500.0);

    private readonly PlanningWorkspaceAutosaveQueue mAutosaveQueue;

    private readonly DelegateCommand mRetryAutosaveCommand;

    private readonly DispatcherTimer mAutosaveSavingIndicatorTimer;

    private EPlanningWorkspaceAutosaveStatus mAutosaveStatus;

    private string mAutosaveStatusText;

    private bool mIsAutosaveSavingIndicatorVisible;

    public EPlanningWorkspaceAutosaveStatus AutosaveStatus
    {
        get
        {
            return mAutosaveStatus;
        }
    }

    public string AutosaveStatusText
    {
        get
        {
            return mAutosaveStatusText;
        }
    }

    public bool IsAutosaveSaving
    {
        get
        {
            return mIsAutosaveSavingIndicatorVisible;
        }
    }

    public bool HasAutosaveError
    {
        get
        {
            return AutosaveStatus == EPlanningWorkspaceAutosaveStatus.Failed;
        }
    }

    public ICommand RetryAutosaveCommand
    {
        get
        {
            return mRetryAutosaveCommand;
        }
    }

    public Task FlushAutosaveAsync(CancellationToken cancellationToken)
    {
        throwIfDisposed();
        return mAutosaveQueue.FlushAsync(cancellationToken);
    }

    public async Task CompleteAutosaveAsync(CancellationToken cancellationToken)
    {
        throwIfDisposed();
        bool shouldRestartRecommendationOnFailure = mRecommendationRefreshTask.IsCompleted == false;
        mRecommendationCancellationSource.Cancel();
        try
        {
            await mRecommendationRefreshTask.WaitAsync(cancellationToken);
            await mAutosaveQueue.CompleteAsync(cancellationToken);
        }
        catch
        {
            if (shouldRestartRecommendationOnFailure && mIsDisposed == false)
            {
                requestRecommendationRefresh();
            }

            throw;
        }
    }

    private void retryAutosave()
    {
        mAutosaveQueue.RequestSave(mSession.Workspace);
    }

    private bool canRetryAutosave()
    {
        return HasAutosaveError;
    }

    private void onAutosaveStateChanged(object? senderOrNull, PlanningWorkspaceAutosaveStateChangedEventArgs eventArgs)
    {
        PlanningWorkspaceAutosaveState state = eventArgs.State;
        Dispatcher.UIThread.Post(
            delegate
            {
                if (mIsDisposed == false)
                {
                    applyAutosaveState(state);
                }
            });
    }

    private void applyAutosaveState(PlanningWorkspaceAutosaveState state)
    {
        mAutosaveStatus = state.Status;
        switch (state.Status)
        {
            case EPlanningWorkspaceAutosaveStatus.Saving:
                mAutosaveStatusText = "저장 중...";
                scheduleAutosaveSavingIndicator();
                break;
            case EPlanningWorkspaceAutosaveStatus.Saved:
                mAutosaveStatusText = string.Empty;
                hideAutosaveSavingIndicator();
                break;
            case EPlanningWorkspaceAutosaveStatus.Failed:
                mAutosaveStatusText = "저장하지 못함";
                hideAutosaveSavingIndicator();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state.Status, "Unknown autosave state.");
        }

        raisePropertyChanged(nameof(AutosaveStatus));
        raisePropertyChanged(nameof(AutosaveStatusText));
        raisePropertyChanged(nameof(HasAutosaveError));
        mRetryAutosaveCommand.NotifyCanExecuteChanged();
    }

    private void scheduleAutosaveSavingIndicator()
    {
        hideAutosaveSavingIndicator();
        mAutosaveSavingIndicatorTimer.Start();
    }

    private void hideAutosaveSavingIndicator()
    {
        mAutosaveSavingIndicatorTimer.Stop();
        if (mIsAutosaveSavingIndicatorVisible == false)
        {
            return;
        }

        mIsAutosaveSavingIndicatorVisible = false;
        raisePropertyChanged(nameof(IsAutosaveSaving));
    }

    private void onAutosaveSavingIndicatorTimerTick(object? senderOrNull, EventArgs eventArguments)
    {
        mAutosaveSavingIndicatorTimer.Stop();
        if (mAutosaveStatus != EPlanningWorkspaceAutosaveStatus.Saving || mIsAutosaveSavingIndicatorVisible)
        {
            return;
        }

        mIsAutosaveSavingIndicatorVisible = true;
        raisePropertyChanged(nameof(IsAutosaveSaving));
    }
}
