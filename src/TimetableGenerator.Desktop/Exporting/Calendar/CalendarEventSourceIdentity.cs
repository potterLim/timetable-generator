using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal readonly record struct CalendarEventSourceIdentity
{
    public string Value { get; }

    public bool IsValid
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public CalendarEventSourceIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Calendar event source identities cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString()
    {
        return Value;
    }
}
