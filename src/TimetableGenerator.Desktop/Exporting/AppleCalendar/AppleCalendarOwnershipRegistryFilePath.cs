using System;
using System.IO;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed record AppleCalendarOwnershipRegistryFilePath
{
    public string Value { get; }

    public AppleCalendarOwnershipRegistryFilePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Apple Calendar ownership registry paths cannot be empty.", nameof(value));
        }

        string fullPath = Path.GetFullPath(value);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)))
        {
            throw new ArgumentException("Apple Calendar ownership registry paths must identify a file.", nameof(value));
        }

        Value = fullPath;
    }
}
