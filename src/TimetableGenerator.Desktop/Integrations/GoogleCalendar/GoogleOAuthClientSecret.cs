using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleOAuthClientSecret
{
    private const int MAXIMUM_LENGTH = 1_024;

    public string Value { get; }

    public GoogleOAuthClientSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > MAXIMUM_LENGTH
            || string.Equals(value, value.Trim(), StringComparison.Ordinal) == false)
        {
            throw new ArgumentException(
                "Google OAuth client secrets must contain a bounded non-whitespace value.",
                nameof(value));
        }

        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                throw new ArgumentException(
                    "Google OAuth client secrets cannot contain control characters.",
                    nameof(value));
            }
        }

        Value = value;
    }

    public override string ToString()
    {
        return "[redacted]";
    }
}
