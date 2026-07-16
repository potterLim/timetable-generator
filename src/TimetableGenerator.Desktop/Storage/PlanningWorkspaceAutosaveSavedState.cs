using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Storage;

public sealed class PlanningWorkspaceAutosaveSavedState
    : PlanningWorkspaceAutosaveState
{
    public PlanningWorkspaceAutosaveSavedState(PlanningWorkspace workspace)
        : base(EPlanningWorkspaceAutosaveStatus.Saved, workspace)
    {
    }
}
