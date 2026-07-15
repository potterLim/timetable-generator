using System;
using System.IO;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed record SchedulePngOutputFilePath
{
    public string Value { get; }

    public string FileName
    {
        get
        {
            return Path.GetFileName(Value);
        }
    }

    internal SchedulePngOutputFilePath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (Path.IsPathFullyQualified(value) == false)
        {
            throw new ArgumentException("PNG output file paths must be fully qualified.", nameof(value));
        }

        if (string.Equals(Path.GetExtension(value), ".png", StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new ArgumentException("PNG output file paths must use the .png extension.", nameof(value));
        }

        Value = Path.GetFullPath(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
