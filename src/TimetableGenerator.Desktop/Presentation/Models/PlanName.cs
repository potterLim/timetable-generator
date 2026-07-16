using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal readonly record struct PlanName
{
    public string Value { get; }

    public PlanName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Plan names cannot be empty.", nameof(value));
        }

        Value = value.Trim();
    }
}
