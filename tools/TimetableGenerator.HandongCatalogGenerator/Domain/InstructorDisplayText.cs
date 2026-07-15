using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed record InstructorDisplayText
{
    public string Value { get; }

    public InstructorDisplayText(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Instructor display text cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
