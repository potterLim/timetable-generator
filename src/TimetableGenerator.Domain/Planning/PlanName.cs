using System;

namespace TimetableGenerator.Domain.Planning;

public sealed record PlanName
{
    public const int MAXIMUM_LENGTH = 80;

    public string Value { get; }

    public PlanName(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Plan names cannot be empty.", nameof(value));
        }

        if (normalizedValue.Length > MAXIMUM_LENGTH)
        {
            throw new ArgumentException("Plan names cannot exceed " + MAXIMUM_LENGTH + " characters.", nameof(value));
        }

        bool hasLineBreak = normalizedValue.Contains('\r') || normalizedValue.Contains('\n');
        if (hasLineBreak)
        {
            throw new ArgumentException("Plan names cannot contain line breaks.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
