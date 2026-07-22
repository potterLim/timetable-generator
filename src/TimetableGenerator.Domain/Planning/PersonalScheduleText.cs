using System;

namespace TimetableGenerator.Domain.Planning;

internal static class PersonalScheduleText
{
    public static string Normalize(string value, int maximumLength, string fieldDescription)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException(fieldDescription + " cannot be empty.", nameof(value));
        }

        if (normalizedValue.Length > maximumLength)
        {
            throw new ArgumentException(
                fieldDescription + " cannot exceed " + maximumLength + " characters.",
                nameof(value));
        }

        if (normalizedValue.Contains('\r') || normalizedValue.Contains('\n'))
        {
            throw new ArgumentException(fieldDescription + " cannot contain line breaks.", nameof(value));
        }

        return normalizedValue;
    }
}
