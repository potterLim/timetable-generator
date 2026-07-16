using System;

namespace TimetableGenerator.Desktop.Storage;

public sealed class PlanningWorkspaceAutosaveException : Exception
{
    public PlanningWorkspaceAutosaveException(Exception innerException)
        : base("The latest planning workspace could not be saved.", innerException)
    {
    }
}
