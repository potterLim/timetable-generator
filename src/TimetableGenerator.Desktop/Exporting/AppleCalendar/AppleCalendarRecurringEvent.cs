using System;
using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarRecurringEvent
{
    private readonly IReadOnlyList<int> mWeekdays;

    public string SourceEventHash { get; }

    public string Fingerprint { get; }

    public string Summary { get; }

    public string Location { get; }

    public string Notes { get; }

    public long StartsAtUnixSeconds { get; }

    public long EndsAtUnixSeconds { get; }

    public long RecurrenceEndsAtUnixSeconds { get; }

    public string TimeZoneIdentifier { get; }

    public IReadOnlyList<int> Weekdays
    {
        get
        {
            return mWeekdays;
        }
    }

    public AppleCalendarRecurringEvent(
        string sourceEventHash,
        string fingerprint,
        string summary,
        string location,
        string notes,
        long startsAtUnixSeconds,
        long endsAtUnixSeconds,
        long recurrenceEndsAtUnixSeconds,
        string timeZoneIdentifier,
        IReadOnlyList<int> weekdays)
    {
        SourceEventHash = requireHash(sourceEventHash, nameof(sourceEventHash));
        Fingerprint = requireHash(fingerprint, nameof(fingerprint));
        Summary = requireText(summary, nameof(summary));
        Location = requireValue(location, nameof(location));
        Notes = requireValue(notes, nameof(notes));
        if (endsAtUnixSeconds <= startsAtUnixSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(endsAtUnixSeconds));
        }

        if (recurrenceEndsAtUnixSeconds < startsAtUnixSeconds)
        {
            throw new ArgumentOutOfRangeException(nameof(recurrenceEndsAtUnixSeconds));
        }

        StartsAtUnixSeconds = startsAtUnixSeconds;
        EndsAtUnixSeconds = endsAtUnixSeconds;
        RecurrenceEndsAtUnixSeconds = recurrenceEndsAtUnixSeconds;
        TimeZoneIdentifier = requireText(timeZoneIdentifier, nameof(timeZoneIdentifier));
        mWeekdays = copyWeekdays(weekdays);
    }

    private static IReadOnlyList<int> copyWeekdays(IReadOnlyList<int> weekdays)
    {
        if (weekdays == null)
        {
            throw new ArgumentNullException(nameof(weekdays));
        }

        List<int> copiedWeekdays = new List<int>(weekdays.Count);
        HashSet<int> uniqueWeekdays = new HashSet<int>();
        foreach (int weekday in weekdays)
        {
            if (weekday < 1 || weekday > 7 || uniqueWeekdays.Add(weekday) == false)
            {
                throw new ArgumentException("Apple Calendar recurrence weekdays must be unique EventKit weekday values.", nameof(weekdays));
            }

            copiedWeekdays.Add(weekday);
        }

        if (copiedWeekdays.Count == 0)
        {
            throw new ArgumentException("Apple Calendar recurring events require at least one weekday.", nameof(weekdays));
        }

        copiedWeekdays.Sort();
        return copiedWeekdays.AsReadOnly();
    }

    private static string requireHash(string value, string parameterName)
    {
        if (value == null || value.Length != 64)
        {
            throw new ArgumentException("Apple Calendar hashes must be lowercase SHA-256 values.", parameterName);
        }

        foreach (char character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                throw new ArgumentException("Apple Calendar hashes must be lowercase SHA-256 values.", parameterName);
            }
        }

        return value;
    }

    private static string requireText(string value, string parameterName)
    {
        string normalizedValue = requireValue(value, parameterName);
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Apple Calendar text values cannot be empty.", parameterName);
        }

        return normalizedValue;
    }

    private static string requireValue(string value, string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return value.Trim().Normalize();
    }
}
