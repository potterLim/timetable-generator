using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarRecurringEventFingerprint
{
    public static string CreateSourceEventHash(string sourceEventId)
    {
        if (string.IsNullOrWhiteSpace(sourceEventId))
        {
            throw new ArgumentException("Apple Calendar source event IDs cannot be empty.", nameof(sourceEventId));
        }

        return createHash(sourceEventId.Trim().Normalize());
    }

    public static string Create(
        string summary,
        string location,
        string notes,
        long startsAtUnixSeconds,
        long endsAtUnixSeconds,
        string timeZoneIdentifier,
        long recurrenceEndsAtUnixSeconds,
        IReadOnlyList<int> weekdays)
    {
        if (weekdays == null)
        {
            throw new ArgumentNullException(nameof(weekdays));
        }

        StringBuilder canonicalValue = new StringBuilder();
        appendText(canonicalValue, summary);
        appendText(canonicalValue, location);
        appendText(canonicalValue, notes);
        canonicalValue.Append(startsAtUnixSeconds.ToString(CultureInfo.InvariantCulture));
        canonicalValue.Append('|');
        canonicalValue.Append(endsAtUnixSeconds.ToString(CultureInfo.InvariantCulture));
        canonicalValue.Append('|');
        appendText(canonicalValue, timeZoneIdentifier);
        canonicalValue.Append(recurrenceEndsAtUnixSeconds.ToString(CultureInfo.InvariantCulture));
        canonicalValue.Append('|');
        for (int index = 0; index < weekdays.Count; ++index)
        {
            if (index > 0)
            {
                canonicalValue.Append(',');
            }

            canonicalValue.Append(weekdays[index].ToString(CultureInfo.InvariantCulture));
        }

        return createHash(canonicalValue.ToString());
    }

    private static void appendText(StringBuilder canonicalValue, string value)
    {
        if (canonicalValue == null)
        {
            throw new ArgumentNullException(nameof(canonicalValue));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value.Trim().Normalize());
        canonicalValue.Append(Convert.ToBase64String(bytes));
        canonicalValue.Append('|');
    }

    private static string createHash(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
