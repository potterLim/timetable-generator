using System;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public sealed class PlanningWorkspaceLoadResult
{
    public EPlanningWorkspaceLoadStatus Status { get; }

    public PlanningWorkspace? WorkspaceOrNull { get; }

    public bool IsFound
    {
        get
        {
            return Status != EPlanningWorkspaceLoadStatus.NotFound;
        }
    }

    private PlanningWorkspaceLoadResult(
        EPlanningWorkspaceLoadStatus status,
        PlanningWorkspace? workspace)
    {
        if (status == EPlanningWorkspaceLoadStatus.NotFound && workspace != null)
        {
            throw new ArgumentException(
                "A not-found workspace result cannot contain a workspace.",
                nameof(workspace));
        }

        if (status != EPlanningWorkspaceLoadStatus.NotFound && workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        Status = status;
        WorkspaceOrNull = workspace;
    }

    public static PlanningWorkspaceLoadResult CreateNotFound()
    {
        return new PlanningWorkspaceLoadResult(
            EPlanningWorkspaceLoadStatus.NotFound,
            null);
    }

    public static PlanningWorkspaceLoadResult CreateLoadedLatestGeneration(
        PlanningWorkspace workspace)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        return new PlanningWorkspaceLoadResult(
            EPlanningWorkspaceLoadStatus.LoadedLatestGeneration,
            workspace);
    }

    public static PlanningWorkspaceLoadResult CreateRecoveredPreviousGeneration(
        PlanningWorkspace workspace)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        return new PlanningWorkspaceLoadResult(
            EPlanningWorkspaceLoadStatus.RecoveredPreviousGeneration,
            workspace);
    }
}
