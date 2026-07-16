using System;

namespace TimetableGenerator.Domain.Catalogs;

public sealed record KoreanCourseName
{
    public string Value { get; }

    public KoreanCourseName(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Korean course names cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
