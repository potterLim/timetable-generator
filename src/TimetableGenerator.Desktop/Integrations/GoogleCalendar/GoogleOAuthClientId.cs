using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleOAuthClientId
{
    private const string DESKTOP_CLIENT_ID_SUFFIX = ".apps.googleusercontent.com";

    public string Value { get; }

    public GoogleOAuthClientId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.EndsWith(
            DESKTOP_CLIENT_ID_SUFFIX,
            StringComparison.Ordinal) == false
            || normalizedValue.Length == DESKTOP_CLIENT_ID_SUFFIX.Length)
        {
            throw new ArgumentException(
                "Google OAuth client IDs must identify a Google desktop client.",
                nameof(value));
        }

        int prefixLength = normalizedValue.Length - DESKTOP_CLIENT_ID_SUFFIX.Length;
        for (int index = 0; index < prefixLength; ++index)
        {
            char character = normalizedValue[index];
            bool isValid = character is >= 'a' and <= 'z'
                || character is >= 'A' and <= 'Z'
                || character is >= '0' and <= '9'
                || character == '-';
            if (isValid == false)
            {
                throw new ArgumentException(
                    "Google OAuth client IDs contain an invalid character.",
                    nameof(value));
            }
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
