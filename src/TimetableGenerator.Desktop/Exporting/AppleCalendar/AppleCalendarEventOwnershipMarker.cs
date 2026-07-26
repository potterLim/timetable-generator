using System;
using System.Security.Cryptography;
using System.Text;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarEventOwnershipMarker
{
    public const string PREFIX = "timetable-generator://managed-event/v1/";

    private const int SHA256_HEX_LENGTH = 64;

    public static string Create(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException(
                "Apple Calendar event ownership markers require an event ID.",
                nameof(eventId));
        }

        byte[] eventIdBytes = Encoding.UTF8.GetBytes(eventId.Trim());
        string eventIdHash = Convert
            .ToHexString(SHA256.HashData(eventIdBytes))
            .ToLowerInvariant();
        return PREFIX + eventIdHash;
    }

    public static bool IsApplicationManaged(string? urlOrNull)
    {
        if (urlOrNull?.StartsWith(PREFIX, StringComparison.Ordinal) != true)
        {
            return false;
        }

        ReadOnlySpan<char> markerPayload = urlOrNull.AsSpan(PREFIX.Length);
        if (markerPayload.Length != SHA256_HEX_LENGTH)
        {
            return false;
        }

        foreach (char value in markerPayload)
        {
            if (value is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}
