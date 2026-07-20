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
    private PlanningWorkspaceConcurrencyToken mConcurrencyToken;
    private Task mWorkerTask;
    private bool mIsWorkerRunning;
    private bool mIsCompletionInProgress;
    private bool mIsCompleted;

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

    public PlanningWorkspaceAutosaveQueue(
        IPlanningWorkspaceStore workspaceStore,
        PlanningWorkspaceConcurrencyToken initialConcurrencyToken)
    {
        if (workspaceStore == null)
        {
            throw new ArgumentNullException(nameof(workspaceStore));
        }

        mSynchronizationRoot = new object();
        mWorkspaceStore = workspaceStore;
        mConcurrencyToken = initialConcurrencyToken;
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
            if (mIsCompletionInProgress || mIsCompleted)
            {
                throw new InvalidOperationException(
                    "Completed autosave queues cannot accept new workspace snapshots.");
            }

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
        Task workerTask;
        lock (mSynchronizationRoot)
        {
            if (mIsCompleted)
            {
                return;
            }

            if (mIsCompletionInProgress)
            {
                throw new InvalidOperationException(
                    "Autosave completion is already in progress.");
            }

            mIsCompletionInProgress = true;
            workerTask = mWorkerTask;
        }

        bool isCompleted = false;
        try
        {
            await workerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            PlanningWorkspaceAutosaveState? stateOrNull = CurrentStateOrNull;
            PlanningWorkspaceAutosaveFailedState? failedStateOrNull =
                stateOrNull as PlanningWorkspaceAutosaveFailedState;
            if (failedStateOrNull != null)
            {
                throw new PlanningWorkspaceAutosaveException(
                    failedStateOrNull.Failure);
            }

            isCompleted = true;
        }
        finally
        {
            lock (mSynchronizationRoot)
            {
                mIsCompletionInProgress = false;
                mIsCompleted = isCompleted;
            }
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
                PlanningWorkspaceConcurrencyToken savedConcurrencyToken =
                    await mWorkspaceStore.SaveAsync(
                        workspaceOrNull,
                        mConcurrencyToken,
                        CancellationToken.None).ConfigureAwait(false);
                mConcurrencyToken = savedConcurrencyToken;
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
