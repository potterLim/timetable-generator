using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class CalendarTimeZoneObservance
{
    public ECalendarTimeZoneObservanceKind Kind { get; }

    public DateTime LocalStart { get; }

    public CalendarUtcOffset OffsetFrom { get; }

    public CalendarUtcOffset OffsetTo { get; }

    public CalendarTimeZoneObservance(ECalendarTimeZoneObservanceKind kind, DateTime localStart, CalendarUtcOffset offsetFrom, CalendarUtcOffset offsetTo)
    {
        if (Enum.IsDefined(kind) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Calendar time-zone observances require a supported kind.");
        }

        if (localStart.Kind != DateTimeKind.Unspecified)
        {
            throw new ArgumentException("Calendar time-zone observances require an unspecified local date and time.", nameof(localStart));
        }

        if (offsetFrom.IsValid == false)
        {
            throw new ArgumentException("Calendar time-zone observances require a valid preceding UTC offset.", nameof(offsetFrom));
        }

        if (offsetTo.IsValid == false)
        {
            throw new ArgumentException("Calendar time-zone observances require a valid resulting UTC offset.", nameof(offsetTo));
        }

        Kind = kind;
        LocalStart = localStart;
        OffsetFrom = offsetFrom;
        OffsetTo = offsetTo;
    }
}
