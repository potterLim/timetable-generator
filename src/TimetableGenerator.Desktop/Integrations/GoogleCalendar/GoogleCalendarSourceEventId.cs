using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleCalendarSourceEventId
{
    private const int MAXIMUM_LENGTH = 256;

    public string Value { get; }

    public GoogleCalendarSourceEventId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0 || normalizedValue.Length > MAXIMUM_LENGTH)
        {
            throw new ArgumentException(
                "Google Calendar source event IDs must contain between 1 and 256 characters.",
                nameof(value));
        }

        if (normalizedValue.Contains('\r') || normalizedValue.Contains('\n'))
        {
            throw new ArgumentException(
                "Google Calendar source event IDs cannot contain line breaks.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
