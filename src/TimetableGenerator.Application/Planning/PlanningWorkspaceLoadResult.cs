using System;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public sealed class PlanningWorkspaceLoadResult
{
    public EPlanningWorkspaceLoadStatus Status { get; }

    public PlanningWorkspace? WorkspaceOrNull { get; }

    public PlanningWorkspaceConcurrencyToken ConcurrencyToken { get; }

    public bool IsFound
    {
        get
        {
            return Status != EPlanningWorkspaceLoadStatus.NotFound;
        }
    }

    private PlanningWorkspaceLoadResult(
        EPlanningWorkspaceLoadStatus status,
        PlanningWorkspace? workspaceOrNull,
        PlanningWorkspaceConcurrencyToken concurrencyToken)
    {
        if (status == EPlanningWorkspaceLoadStatus.NotFound && workspaceOrNull != null)
        {
            throw new ArgumentException(
                "A not-found workspace result cannot contain a workspace.",
                nameof(workspaceOrNull));
        }

        if (status != EPlanningWorkspaceLoadStatus.NotFound && workspaceOrNull == null)
        {
            throw new ArgumentNullException(nameof(workspaceOrNull));
        }

        if (status == EPlanningWorkspaceLoadStatus.NotFound
            && concurrencyToken.RepresentsMissingWorkspace == false)
        {
            throw new ArgumentException(
                "A not-found workspace result requires the missing-workspace token.",
                nameof(concurrencyToken));
        }

        if (status != EPlanningWorkspaceLoadStatus.NotFound
            && concurrencyToken.RepresentsMissingWorkspace)
        {
            throw new ArgumentException(
                "A found workspace result requires a persisted concurrency token.",
                nameof(concurrencyToken));
        }

        Status = status;
        WorkspaceOrNull = workspaceOrNull;
        ConcurrencyToken = concurrencyToken;
    }

    public static PlanningWorkspaceLoadResult CreateNotFound()
    {
        return new PlanningWorkspaceLoadResult(
            EPlanningWorkspaceLoadStatus.NotFound,
            null,
            PlanningWorkspaceConcurrencyToken.MissingWorkspace);
    }

    public static PlanningWorkspaceLoadResult CreateLoadedLatestGeneration(
        PlanningWorkspace workspace,
        PlanningWorkspaceConcurrencyToken concurrencyToken)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        return new PlanningWorkspaceLoadResult(
            EPlanningWorkspaceLoadStatus.LoadedLatestGeneration,
            workspace,
            concurrencyToken);
    }

    public static PlanningWorkspaceLoadResult CreateRecoveredPreviousGeneration(
        PlanningWorkspace workspace,
        PlanningWorkspaceConcurrencyToken concurrencyToken)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        return new PlanningWorkspaceLoadResult(
            EPlanningWorkspaceLoadStatus.RecoveredPreviousGeneration,
            workspace,
            concurrencyToken);
    }
}
