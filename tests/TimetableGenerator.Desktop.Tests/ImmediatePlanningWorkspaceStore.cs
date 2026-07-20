using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests;

internal sealed class ImmediatePlanningWorkspaceStore : IPlanningWorkspaceStore
{
    private PlanningWorkspaceConcurrencyToken mConcurrencyToken =
        PlanningWorkspaceConcurrencyToken.MissingWorkspace;

    public PlanningWorkspace? LastSavedWorkspaceOrNull { get; private set; }

    public Task<PlanningWorkspaceLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (LastSavedWorkspaceOrNull == null)
        {
            return Task.FromResult(PlanningWorkspaceLoadResult.CreateNotFound());
        }

        return Task.FromResult(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(
                LastSavedWorkspaceOrNull,
                mConcurrencyToken));
    }

    public Task<PlanningWorkspaceConcurrencyToken> SaveAsync(
        PlanningWorkspace workspace,
        PlanningWorkspaceConcurrencyToken expectedToken,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedToken != mConcurrencyToken)
        {
            throw new PlanningWorkspaceConcurrencyException(
                expectedToken,
                mConcurrencyToken);
        }

        LastSavedWorkspaceOrNull = workspace;
        mConcurrencyToken = mConcurrencyToken.GetNext();
        return Task.FromResult(mConcurrencyToken);
    }
}
