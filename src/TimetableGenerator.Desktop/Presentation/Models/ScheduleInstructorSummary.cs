using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed record ScheduleInstructorSummary
{
    public string Value { get; }

    public ScheduleInstructorSummary(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException(
                "Schedule instructor summaries cannot be empty.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
