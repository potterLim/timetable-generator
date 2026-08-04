using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal readonly record struct CalendarTimeZoneId
{
    private readonly bool mIsInitialized;

    public string Value { get; }

    public bool IsValid
    {
        get
        {
            return mIsInitialized && string.IsNullOrWhiteSpace(Value) == false;
        }
    }

    public CalendarTimeZoneId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Calendar time-zone IDs cannot be empty.", nameof(value));
        }

        string normalizedValue = value.Trim();
        string? windowsTimeZoneIdOrNull;
        bool hasWindowsTimeZoneEquivalent = TimeZoneInfo.TryConvertIanaIdToWindowsId(normalizedValue, out windowsTimeZoneIdOrNull);
        if (hasWindowsTimeZoneEquivalent == false || string.IsNullOrWhiteSpace(windowsTimeZoneIdOrNull))
        {
            throw new ArgumentException("Calendar time-zone IDs must be IANA identifiers supported on every target platform.", nameof(value));
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(normalizedValue);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new ArgumentException("The calendar time-zone ID is not installed on this system.", nameof(value), exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new ArgumentException("The calendar time-zone ID has invalid transition data.", nameof(value), exception);
        }

        Value = normalizedValue;
        mIsInitialized = true;
    }

    public static CalendarTimeZoneId CreateFromSystemTimeZone(TimeZoneInfo timeZone)
    {
        if (timeZone == null)
        {
            throw new ArgumentNullException(nameof(timeZone));
        }

        if (timeZone.HasIanaId)
        {
            return new CalendarTimeZoneId(timeZone.Id);
        }

        string? ianaTimeZoneIdOrNull;
        bool hasIanaTimeZoneId = TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone.Id, out ianaTimeZoneIdOrNull);
        if (hasIanaTimeZoneId == false || string.IsNullOrWhiteSpace(ianaTimeZoneIdOrNull))
        {
            throw new ArgumentException("The system time zone cannot be represented by a portable IANA identifier.", nameof(timeZone));
        }

        return new CalendarTimeZoneId(ianaTimeZoneIdOrNull);
    }

    public CalendarUtcOffset FindUtcOffset(DateOnly date, TimeOnly time)
    {
        DateTime localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        TimeZoneInfo timeZone = findSystemTimeZone();
        if (timeZone.IsInvalidTime(localDateTime))
        {
            throw new InvalidOperationException("The calendar local time does not exist in time zone " + Value + ".");
        }

        if (timeZone.IsAmbiguousTime(localDateTime))
        {
            return new CalendarUtcOffset(findFirstOccurrenceUtcOffset(timeZone, localDateTime));
        }

        return new CalendarUtcOffset(timeZone.GetUtcOffset(localDateTime));
    }

    public DateTimeOffset ResolveLocalDateTime(DateOnly date, TimeOnly time)
    {
        DateTime localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        CalendarUtcOffset utcOffset = FindUtcOffset(date, time);
        return new DateTimeOffset(localDateTime, utcOffset.Value);
    }

    public override string ToString()
    {
        return Value;
    }

    internal TimeZoneInfo findSystemTimeZone()
    {
        if (IsValid == false)
        {
            throw new InvalidOperationException("An uninitialized calendar time-zone ID cannot be resolved.");
        }

        return TimeZoneInfo.FindSystemTimeZoneById(Value);
    }

    private static TimeSpan findFirstOccurrenceUtcOffset(TimeZoneInfo timeZone, DateTime ambiguousLocalDateTime)
    {
        TimeSpan[] possibleOffsets = timeZone.GetAmbiguousTimeOffsets(ambiguousLocalDateTime);
        if (possibleOffsets[0] > possibleOffsets[1])
        {
            return possibleOffsets[0];
        }

        return possibleOffsets[1];
    }
}
