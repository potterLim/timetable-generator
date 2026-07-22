using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GooglePkceCodeChallenge
{
    public string Value { get; }

    public GooglePkceCodeChallenge(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Google PKCE code challenges cannot be empty.", nameof(value));
        }

        Value = value;
    }
}
