using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal readonly record struct CalendarTimeZoneId
{
    public string Value { get; }

    public bool IsValid
    {
        get
        {
            return string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public CalendarTimeZoneId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Calendar time-zone IDs cannot be empty.",
                nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString()
    {
        return Value;
    }
}
