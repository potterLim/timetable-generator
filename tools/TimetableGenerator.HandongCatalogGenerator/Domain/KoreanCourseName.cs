using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed record KoreanCourseName
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
            throw new ArgumentException("The Korean course name cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
