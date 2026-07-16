using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Storage;

public sealed class PlanningWorkspaceAutosaveSavingState
    : PlanningWorkspaceAutosaveState
{
    public PlanningWorkspaceAutosaveSavingState(PlanningWorkspace workspace)
        : base(EPlanningWorkspaceAutosaveStatus.Saving, workspace)
    {
    }
}
