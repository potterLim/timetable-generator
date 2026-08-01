using System;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarRegistryValue
{
    public static string RequireHash(string value, string parameterName)
    {
        if (value == null || value.Length != 64)
        {
            throw new ArgumentException("Apple Calendar registry hashes must be lowercase SHA-256 values.", parameterName);
        }

        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                throw new ArgumentException("Apple Calendar registry hashes must be lowercase SHA-256 values.", parameterName);
            }
        }

        return value;
    }

    public static string RequirePlanId(string value, string parameterName)
    {
        Guid planId;
        if (Guid.TryParseExact(value, "D", out planId) == false || planId == Guid.Empty)
        {
            throw new ArgumentException("Apple Calendar registry plan IDs must be canonical non-empty GUIDs.", parameterName);
        }

        return planId.ToString("D");
    }

    public static string RequireGuid(string value, string parameterName)
    {
        Guid identifier;
        if (Guid.TryParseExact(value, "D", out identifier) == false || identifier == Guid.Empty)
        {
            throw new ArgumentException("Apple Calendar registry operation IDs must be canonical non-empty GUIDs.", parameterName);
        }

        return identifier.ToString("D");
    }

    public static string RequireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Apple Calendar registry text values cannot be empty.", parameterName);
        }

        return value.Trim().Normalize();
    }

    public static string? NormalizeOptionalText(string? valueOrNull)
    {
        return string.IsNullOrWhiteSpace(valueOrNull) ? null : valueOrNull.Trim().Normalize();
    }
}
