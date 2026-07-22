using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GooglePkceCodeVerifier
{
    public string Value { get; }

    public GooglePkceCodeVerifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Google PKCE code verifiers cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString()
    {
        return "[redacted]";
    }
}
