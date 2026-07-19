using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal static class CalendarEventUidFactory
{
    public static CalendarEventUid Create(
        PlanId planId,
        CalendarEventSourceIdentity sourceIdentity,
        DailyTimeRange timeRange)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Calendar event UID creation requires a valid plan ID.",
                nameof(planId));
        }

        if (sourceIdentity.IsValid == false)
        {
            throw new ArgumentException(
                "Calendar event UID creation requires a valid source identity.",
                nameof(sourceIdentity));
        }

        if (timeRange.IsValid == false)
        {
            throw new ArgumentException(
                "Calendar event UID creation requires a valid daily time range.",
                nameof(timeRange));
        }

        string identityText = string.Join(
            "|",
            planId.ToString(),
            sourceIdentity.Value,
            timeRange.Start.MinutesFromMidnight.ToString(
                CultureInfo.InvariantCulture),
            timeRange.End.MinutesFromMidnight.ToString(
                CultureInfo.InvariantCulture));
        byte[] identityBytes = Encoding.UTF8.GetBytes(identityText);
        byte[] identityHash = SHA256.HashData(identityBytes);
        string hashText = Convert.ToHexString(identityHash).ToLowerInvariant();
        return new CalendarEventUid(hashText);
    }
}
