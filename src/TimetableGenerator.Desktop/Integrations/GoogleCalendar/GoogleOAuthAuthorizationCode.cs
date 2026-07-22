using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleOAuthAuthorizationCode
{
    public string Value { get; }

    public GoogleOAuthAuthorizationCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Google OAuth authorization codes cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public override string ToString()
    {
        return "[redacted]";
    }
}
