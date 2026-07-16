using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Threading;

using TimetableGenerator.Desktop.Storage;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private readonly PlanningWorkspaceAutosaveQueue mAutosaveQueue;

    private readonly DelegateCommand mRetryAutosaveCommand;

    private EPlanningWorkspaceAutosaveStatus mAutosaveStatus;

    private string mAutosaveStatusText;

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

    public bool IsAutosaveSaved
    {
        get
        {
            return AutosaveStatus == EPlanningWorkspaceAutosaveStatus.Saved;
        }
    }

    public bool IsAutosaveSaving
    {
        get
        {
            return AutosaveStatus == EPlanningWorkspaceAutosaveStatus.Saving;
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

    public Task CompleteAutosaveAsync(CancellationToken cancellationToken)
    {
        throwIfDisposed();
        return mAutosaveQueue.CompleteAsync(cancellationToken);
    }

    private void retryAutosave()
    {
        mAutosaveQueue.RequestSave(mSession.Workspace);
    }

    private bool canRetryAutosave()
    {
        return HasAutosaveError;
    }

    private void onAutosaveStateChanged(
        object? senderOrNull,
        PlanningWorkspaceAutosaveStateChangedEventArgs eventArgs)
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
                break;
            case EPlanningWorkspaceAutosaveStatus.Saved:
                mAutosaveStatusText = "자동 저장됨";
                break;
            case EPlanningWorkspaceAutosaveStatus.Failed:
                mAutosaveStatusText = "저장하지 못함";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state.Status,
                    "Unknown autosave state.");
        }

        raisePropertyChanged(nameof(AutosaveStatus));
        raisePropertyChanged(nameof(AutosaveStatusText));
        raisePropertyChanged(nameof(IsAutosaveSaved));
        raisePropertyChanged(nameof(IsAutosaveSaving));
        raisePropertyChanged(nameof(HasAutosaveError));
        mRetryAutosaveCommand.NotifyCanExecuteChanged();
    }
}
