using System;
using System.IO;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleCalendarExportLockFilePath
{
    public string Value { get; }

    public GoogleCalendarExportLockFilePath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Value = Path.GetFullPath(value);
    }
}
