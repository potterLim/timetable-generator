using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleOAuthState
{
    public string Value { get; }

    public GoogleOAuthState(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Google OAuth state cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString()
    {
        return "[redacted]";
    }
}
