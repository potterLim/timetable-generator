using System;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Storage;

public sealed class PlanningWorkspaceAutosaveFailedState
    : PlanningWorkspaceAutosaveState
{
    public Exception Failure { get; }

    public PlanningWorkspaceAutosaveFailedState(
        PlanningWorkspace workspace,
        Exception failure)
        : base(EPlanningWorkspaceAutosaveStatus.Failed, workspace)
    {
        if (failure == null)
        {
            throw new ArgumentNullException(nameof(failure));
        }

        Failure = failure;
    }
}
