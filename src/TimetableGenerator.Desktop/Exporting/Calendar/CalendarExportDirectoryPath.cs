using System;
using System.IO;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed record CalendarExportDirectoryPath
{
    public string Value { get; }

    public CalendarExportDirectoryPath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Calendar export directory paths cannot be empty.",
                nameof(value));
        }

        if (Path.IsPathFullyQualified(value) == false)
        {
            throw new ArgumentException(
                "Calendar export directory paths must be fully qualified.",
                nameof(value));
        }

        string fullPath = Path.GetFullPath(value);
        Value = fullPath;
    }

    public override string ToString()
    {
        return Value;
    }
}
