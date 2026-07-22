using System;
using System.IO;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleCalendarOAuthConfigurationPath
{
    public string Value { get; }

    public GoogleCalendarOAuthConfigurationPath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Google Calendar OAuth configuration paths cannot be empty.",
                nameof(value));
        }

        string fullPath = Path.GetFullPath(value);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath))
            || Directory.Exists(fullPath))
        {
            throw new ArgumentException(
                "Google Calendar OAuth configuration paths must identify a file.",
                nameof(value));
        }

        Value = fullPath;
    }

    public override string ToString()
    {
        return Value;
    }
}
