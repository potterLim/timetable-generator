using System;
using System.IO;

namespace TimetableGenerator.Infrastructure.Exporting;

public readonly record struct ScheduleExportDirectoryPath
{
    public string Value { get; }

    public bool IsValid
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false && Path.IsPathFullyQualified(Value);
        }
    }

    public ScheduleExportDirectoryPath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Export directory paths cannot be empty.", nameof(value));
        }

        Value = Path.GetFullPath(normalizedValue);
    }

    public override string ToString()
    {
        if (IsValid == false)
        {
            return string.Empty;
        }

        return Value;
    }
}
