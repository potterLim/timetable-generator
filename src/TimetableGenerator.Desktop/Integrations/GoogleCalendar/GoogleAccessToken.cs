using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleAccessToken
{
    public string Value { get; }

    public GoogleAccessToken(string value)
    {
        Value = validate(value, "Google access tokens cannot be empty.");
    }

    private static string validate(string value, string message)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException(message, nameof(value));
        }

        return normalizedValue;
    }

    public override string ToString()
    {
        return "[redacted]";
    }
}
