using System;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed record AppleCalendarId
{
    public string Value { get; }

    public AppleCalendarId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Apple Calendar IDs cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
