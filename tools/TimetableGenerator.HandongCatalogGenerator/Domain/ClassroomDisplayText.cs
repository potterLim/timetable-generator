using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed record ClassroomDisplayText
{
    public string Value { get; }

    public ClassroomDisplayText(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Classroom display text cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
