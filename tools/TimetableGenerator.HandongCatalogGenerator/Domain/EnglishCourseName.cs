using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed record EnglishCourseName
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
            throw new ArgumentException("The English course name cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
