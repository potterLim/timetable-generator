using System;
using System.IO;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed record SchedulePngExportArtifactFilePath
{
    public string Value { get; }

    internal SchedulePngExportArtifactFilePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Export artifact file paths cannot be empty.",
                nameof(value));
        }

        if (Path.IsPathFullyQualified(value) == false)
        {
            throw new ArgumentException(
                "Export artifact file paths must be fully qualified.",
                nameof(value));
        }

        Value = Path.GetFullPath(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
