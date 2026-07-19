using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleCalendarId
{
    public string Value { get; }

    public GoogleCalendarId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Google Calendar IDs cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
