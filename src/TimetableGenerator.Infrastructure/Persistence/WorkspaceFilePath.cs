using System;
using System.IO;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed record WorkspaceFilePath
{
    public string Value { get; }

    public WorkspaceFilePath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Workspace file paths cannot be empty.",
                nameof(value));
        }

        Value = Path.GetFullPath(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
