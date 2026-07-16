using System;

namespace TimetableGenerator.Domain.Catalogs;

public sealed record EnglishCourseName
{
    public string Value { get; }

    public EnglishCourseName(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("English course names cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
