using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests;

internal sealed class ImmediatePlanningWorkspaceStore : IPlanningWorkspaceStore
{
    public PlanningWorkspace? LastSavedWorkspaceOrNull { get; private set; }

    public Task<PlanningWorkspaceLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(PlanningWorkspaceLoadResult.CreateNotFound());
    }

    public Task SaveAsync(
        PlanningWorkspace workspace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastSavedWorkspaceOrNull = workspace;
        return Task.CompletedTask;
    }
}
