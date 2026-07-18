using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseSearchQuery
{
    public string Value { get; }

    public bool IsEmpty
    {
        get
        {
            return Value.Length == 0;
        }
    }

    private CourseSearchQuery(string value)
    {
        Value = value;
    }

    public static CourseSearchQuery Create(string sourceText)
    {
        if (sourceText == null)
        {
            throw new ArgumentNullException(nameof(sourceText));
        }

        return new CourseSearchQuery(sourceText.Trim());
    }
}
