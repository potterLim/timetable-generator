using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Storage;

public sealed class PlanningWorkspaceAutosaveQueue
{
    private readonly object mSynchronizationRoot;
    private readonly IPlanningWorkspaceStore mWorkspaceStore;

    private PlanningWorkspace? mPendingWorkspaceOrNull;
    private PlanningWorkspaceAutosaveState? mCurrentStateOrNull;
    private Task mWorkerTask;
    private bool mIsWorkerRunning;

    public event EventHandler<PlanningWorkspaceAutosaveStateChangedEventArgs>? StateChanged;

    public PlanningWorkspaceAutosaveState? CurrentStateOrNull
    {
        get
        {
            lock (mSynchronizationRoot)
            {
                return mCurrentStateOrNull;
            }
        }
    }

    public PlanningWorkspaceAutosaveQueue(IPlanningWorkspaceStore workspaceStore)
    {
        if (workspaceStore == null)
        {
            throw new ArgumentNullException(nameof(workspaceStore));
        }

        mSynchronizationRoot = new object();
        mWorkspaceStore = workspaceStore;
        mWorkerTask = Task.CompletedTask;
    }

    public void RequestSave(PlanningWorkspace workspace)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        lock (mSynchronizationRoot)
        {
            mPendingWorkspaceOrNull = workspace;
            if (mIsWorkerRunning)
            {
                return;
            }

            mIsWorkerRunning = true;
            mWorkerTask = Task.Run(runSaveLoopAsync);
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        Task workerTask;
        lock (mSynchronizationRoot)
        {
            workerTask = mWorkerTask;
        }

        return workerTask.WaitAsync(cancellationToken);
    }

    public async Task CompleteAsync(CancellationToken cancellationToken)
    {
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        PlanningWorkspaceAutosaveState? stateOrNull = CurrentStateOrNull;
        PlanningWorkspaceAutosaveFailedState? failedStateOrNull =
            stateOrNull as PlanningWorkspaceAutosaveFailedState;
        if (failedStateOrNull != null)
        {
            throw new PlanningWorkspaceAutosaveException(failedStateOrNull.Failure);
        }
    }

    private async Task runSaveLoopAsync()
    {
        while (true)
        {
            PlanningWorkspace? workspaceOrNull = takePendingWorkspaceOrNull();
            if (workspaceOrNull == null)
            {
                return;
            }

            publishState(new PlanningWorkspaceAutosaveSavingState(workspaceOrNull));

            try
            {
                await mWorkspaceStore.SaveAsync(
                    workspaceOrNull,
                    CancellationToken.None).ConfigureAwait(false);
                publishState(new PlanningWorkspaceAutosaveSavedState(workspaceOrNull));
            }
            catch (Exception failure)
            {
                publishState(
                    new PlanningWorkspaceAutosaveFailedState(workspaceOrNull, failure));
            }
        }
    }

    private PlanningWorkspace? takePendingWorkspaceOrNull()
    {
        lock (mSynchronizationRoot)
        {
            PlanningWorkspace? workspaceOrNull = mPendingWorkspaceOrNull;
            if (workspaceOrNull == null)
            {
                mIsWorkerRunning = false;
                return null;
            }

            mPendingWorkspaceOrNull = null;
            return workspaceOrNull;
        }
    }

    private void publishState(PlanningWorkspaceAutosaveState state)
    {
        EventHandler<PlanningWorkspaceAutosaveStateChangedEventArgs>?
            stateChangedHandlerOrNull;
        lock (mSynchronizationRoot)
        {
            mCurrentStateOrNull = state;
            stateChangedHandlerOrNull = StateChanged;
        }

        if (stateChangedHandlerOrNull == null)
        {
            return;
        }

        PlanningWorkspaceAutosaveStateChangedEventArgs eventArguments =
            new PlanningWorkspaceAutosaveStateChangedEventArgs(state);
        Delegate[] stateChangedHandlers = stateChangedHandlerOrNull.GetInvocationList();
        foreach (Delegate stateChangedHandler in stateChangedHandlers)
        {
            try
            {
                EventHandler<PlanningWorkspaceAutosaveStateChangedEventArgs> typedHandler =
                    (EventHandler<PlanningWorkspaceAutosaveStateChangedEventArgs>)
                    stateChangedHandler;
                typedHandler(this, eventArguments);
            }
            catch (Exception notificationFailure)
            {
                Trace.TraceError(
                    "A planning workspace autosave state subscriber failed: {0}",
                    notificationFailure);
            }
        }
    }
}
