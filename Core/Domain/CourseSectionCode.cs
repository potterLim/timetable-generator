using System;

namespace TimetableGenerator.Core.Domain;

public sealed record CourseSectionCode
{
    private const string DEFAULT_SECTION_CODE = "00";

    public string Value { get; }

    public bool IsDefault
    {
        get
        {
            return string.Equals(Value, DEFAULT_SECTION_CODE, StringComparison.Ordinal);
        }
    }

    public CourseSectionCode(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Course section codes cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
