using System;
using System.Security.Cryptography;
using System.Text;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarEventOwnershipMarker
{
    public const string LEGACY_PREFIX = "timetable-generator://managed-event/v1/";

    public const string PREFIX = "timetable-generator://managed-event/v2/";

    private const int SHA256_HEX_LENGTH = 64;

    public static string Create(PlanId planId, string eventId)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Apple Calendar event ownership markers require a valid plan ID.",
                nameof(planId));
        }

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
        return PREFIX + planId.Value.ToString("D") + "/" + eventIdHash;
    }

    public static bool IsApplicationManaged(string? urlOrNull)
    {
        return TryParsePlanIdOrNull(urlOrNull) != null
            || isLegacyMarker(urlOrNull);
    }

    public static bool IsManagedByPlan(string? urlOrNull, PlanId planId)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Apple Calendar event ownership checks require a valid plan ID.",
                nameof(planId));
        }

        return TryParsePlanIdOrNull(urlOrNull) == planId;
    }

    public static PlanId? TryParsePlanIdOrNull(string? urlOrNull)
    {
        if (urlOrNull?.StartsWith(PREFIX, StringComparison.Ordinal) != true)
        {
            return null;
        }

        ReadOnlySpan<char> markerPayload = urlOrNull.AsSpan(PREFIX.Length);
        int separatorIndex = markerPayload.IndexOf('/');
        if (separatorIndex <= 0)
        {
            return null;
        }

        ReadOnlySpan<char> planIdPayload = markerPayload[..separatorIndex];
        ReadOnlySpan<char> eventHashPayload = markerPayload[(separatorIndex + 1)..];
        Guid planIdValue;
        if (Guid.TryParseExact(planIdPayload, "D", out planIdValue) == false
            || planIdValue == Guid.Empty
            || isCanonicalHash(eventHashPayload) == false)
        {
            return null;
        }

        return new PlanId(planIdValue);
    }

    private static bool isLegacyMarker(string? urlOrNull)
    {
        return urlOrNull?.StartsWith(
                LEGACY_PREFIX,
                StringComparison.Ordinal) == true
            && isCanonicalHash(
                urlOrNull.AsSpan(LEGACY_PREFIX.Length));
    }

    private static bool isCanonicalHash(ReadOnlySpan<char> value)
    {
        if (value.Length != SHA256_HEX_LENGTH)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}
