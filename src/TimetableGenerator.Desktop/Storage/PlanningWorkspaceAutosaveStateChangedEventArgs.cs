using System;

namespace TimetableGenerator.Desktop.Storage;

public sealed class PlanningWorkspaceAutosaveStateChangedEventArgs : EventArgs
{
    public PlanningWorkspaceAutosaveState State { get; }

    public PlanningWorkspaceAutosaveStateChangedEventArgs(
        PlanningWorkspaceAutosaveState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        State = state;
    }
}
