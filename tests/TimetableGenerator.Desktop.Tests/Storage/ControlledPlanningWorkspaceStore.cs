using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests.Storage;

internal sealed class ControlledPlanningWorkspaceStore : IPlanningWorkspaceStore
{
    private readonly object mSynchronizationRoot;
    private readonly Queue<ControlledSaveAttempt> mPendingAttempts;
    private readonly List<PlanningWorkspace> mStartedWorkspaces;
    private readonly List<CancellationToken> mSaveCancellationTokens;

    private int mActiveSaveCount;
    private int mMaximumConcurrentSaveCount;

    public IReadOnlyList<PlanningWorkspace> StartedWorkspaces
    {
        get
        {
            lock (mSynchronizationRoot)
            {
                return mStartedWorkspaces.ToArray();
            }
        }
    }

    public IReadOnlyList<CancellationToken> SaveCancellationTokens
    {
        get
        {
            lock (mSynchronizationRoot)
            {
                return mSaveCancellationTokens.ToArray();
            }
        }
    }

    public int MaximumConcurrentSaveCount
    {
        get
        {
            lock (mSynchronizationRoot)
            {
                return mMaximumConcurrentSaveCount;
            }
        }
    }

    public ControlledPlanningWorkspaceStore()
    {
        mSynchronizationRoot = new object();
        mPendingAttempts = new Queue<ControlledSaveAttempt>();
        mStartedWorkspaces = new List<PlanningWorkspace>();
        mSaveCancellationTokens = new List<CancellationToken>();
    }

    public void EnqueueSaveAttempt(ControlledSaveAttempt saveAttempt)
    {
        if (saveAttempt == null)
        {
            throw new ArgumentNullException(nameof(saveAttempt));
        }

        lock (mSynchronizationRoot)
        {
            mPendingAttempts.Enqueue(saveAttempt);
        }
    }

    public Task<PlanningWorkspaceLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(PlanningWorkspaceLoadResult.CreateNotFound());
    }

    public async Task SaveAsync(
        PlanningWorkspace workspace,
        CancellationToken cancellationToken)
    {
        ControlledSaveAttempt saveAttempt;
        lock (mSynchronizationRoot)
        {
            if (mPendingAttempts.Count == 0)
            {
                throw new InvalidOperationException(
                    "The test store received an unconfigured save attempt.");
            }

            saveAttempt = mPendingAttempts.Dequeue();
            mStartedWorkspaces.Add(workspace);
            mSaveCancellationTokens.Add(cancellationToken);
            ++mActiveSaveCount;
            if (mActiveSaveCount > mMaximumConcurrentSaveCount)
            {
                mMaximumConcurrentSaveCount = mActiveSaveCount;
            }
        }

        saveAttempt.markStarted(workspace);
        try
        {
            await saveAttempt.waitForCompletionAsync().ConfigureAwait(false);
        }
        finally
        {
            lock (mSynchronizationRoot)
            {
                --mActiveSaveCount;
            }
        }
    }
}
