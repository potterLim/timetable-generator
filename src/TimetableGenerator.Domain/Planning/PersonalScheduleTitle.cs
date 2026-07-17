using System;

namespace TimetableGenerator.Domain.Planning;

public sealed record PersonalScheduleTitle
{
    public const int MAXIMUM_LENGTH = 80;

    public string Value { get; }

    public PersonalScheduleTitle(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException(
                "Personal schedule titles cannot be empty.",
                nameof(value));
        }

        if (normalizedValue.Length > MAXIMUM_LENGTH)
        {
            throw new ArgumentException(
                "Personal schedule titles cannot exceed 80 characters.",
                nameof(value));
        }

        if (normalizedValue.Contains('\r') || normalizedValue.Contains('\n'))
        {
            throw new ArgumentException(
                "Personal schedule titles cannot contain line breaks.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
