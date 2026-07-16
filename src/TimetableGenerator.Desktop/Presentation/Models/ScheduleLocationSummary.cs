using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed record ScheduleLocationSummary
{
    public string Value { get; }

    public ScheduleLocationSummary(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException(
                "Schedule location summaries cannot be empty.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
