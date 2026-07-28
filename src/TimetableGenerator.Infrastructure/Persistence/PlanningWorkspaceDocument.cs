using System;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed class PlanningWorkspaceDocument
{
    public WorkspaceGeneration Generation { get; }

    public PlanningWorkspace Workspace { get; }

    public PlanningWorkspaceDocument(WorkspaceGeneration generation, PlanningWorkspace workspace)
    {
        if (generation.IsValid == false)
        {
            throw new ArgumentException("Planning workspace documents require a valid generation.", nameof(generation));
        }

        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        Generation = generation;
        Workspace = workspace;
    }
}
