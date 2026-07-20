using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests.Product.Loading;

internal sealed class RecordingPlanningWorkspaceStore : IPlanningWorkspaceStore
{
    private readonly List<PlanningWorkspace> mSavedWorkspaces;

    private PlanningWorkspaceLoadResult mLoadResult;

    public IReadOnlyList<PlanningWorkspace> SavedWorkspaces
    {
        get
        {
            return mSavedWorkspaces.ToArray();
        }
    }

    public int LoadCount { get; private set; }

    public RecordingPlanningWorkspaceStore(PlanningWorkspaceLoadResult loadResult)
    {
        if (loadResult == null)
        {
            throw new ArgumentNullException(nameof(loadResult));
        }

        mLoadResult = loadResult;
        mSavedWorkspaces = new List<PlanningWorkspace>();
    }

    public Task<PlanningWorkspaceLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ++LoadCount;
        return Task.FromResult(mLoadResult);
    }

    public Task<PlanningWorkspaceConcurrencyToken> SaveAsync(
        PlanningWorkspace workspace,
        PlanningWorkspaceConcurrencyToken expectedToken,
        CancellationToken cancellationToken)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        cancellationToken.ThrowIfCancellationRequested();
        PlanningWorkspaceConcurrencyToken actualToken = mLoadResult.ConcurrencyToken;
        if (expectedToken != actualToken)
        {
            throw new PlanningWorkspaceConcurrencyException(
                expectedToken,
                actualToken);
        }

        PlanningWorkspaceConcurrencyToken savedToken = actualToken.GetNext();
        mSavedWorkspaces.Add(workspace);
        mLoadResult = PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(
            workspace,
            savedToken);
        return Task.FromResult(savedToken);
    }
}
