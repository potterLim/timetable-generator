using System;
using System.IO;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleCalendarBindingFilePath
{
    public string Value { get; }

    public GoogleCalendarBindingFilePath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string fullPath = Path.GetFullPath(value);
        if (string.Equals(
            Path.GetExtension(fullPath),
            ".json",
            StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new ArgumentException(
                "Google Calendar binding paths must identify a JSON file.",
                nameof(value));
        }

        Value = fullPath;
    }
}
