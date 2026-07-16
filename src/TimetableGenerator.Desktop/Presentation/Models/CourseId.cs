using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal readonly record struct CourseId
{
    public string Value { get; }

    public CourseId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Course IDs cannot be empty.", nameof(value));
        }

        Value = value;
    }
}
