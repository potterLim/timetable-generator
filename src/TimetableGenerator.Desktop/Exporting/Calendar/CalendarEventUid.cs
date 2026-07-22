using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal readonly record struct CalendarEventUid
{
    public string Value { get; }

    public bool IsValid
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public CalendarEventUid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Calendar event UIDs cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString()
    {
        return Value;
    }
}
