using System;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Storage;

public abstract class PlanningWorkspaceAutosaveState
{
    public EPlanningWorkspaceAutosaveStatus Status { get; }

    public PlanningWorkspace Workspace { get; }

    private protected PlanningWorkspaceAutosaveState(
        EPlanningWorkspaceAutosaveStatus status,
        PlanningWorkspace workspace)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        Status = status;
        Workspace = workspace;
    }
}
