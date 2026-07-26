using System;
using System.IO;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed record AppleCalendarExportLockFilePath
{
    public string Value { get; }

    public AppleCalendarExportLockFilePath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Value = Path.GetFullPath(value);
    }
}
