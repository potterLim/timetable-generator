using System;

namespace TimetableGenerator.Infrastructure.Persistence;

internal sealed class WorkspaceGenerationFile
{
    public WorkspaceGeneration Generation { get; }

    public string Path { get; }

    public WorkspaceGenerationFile(WorkspaceGeneration generation, string path)
    {
        if (generation.IsValid == false)
        {
            throw new ArgumentException(
                "Workspace generation files require a valid generation.",
                nameof(generation));
        }

        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        Generation = generation;
        Path = path;
    }
}
