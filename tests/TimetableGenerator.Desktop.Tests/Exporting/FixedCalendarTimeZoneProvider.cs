using System;

using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Tests.Exporting;

internal sealed class FixedCalendarTimeZoneProvider : ICalendarTimeZoneProvider
{
    private readonly CalendarTimeZoneId mTimeZoneId;

    public FixedCalendarTimeZoneProvider(CalendarTimeZoneId timeZoneId)
    {
        if (timeZoneId.IsValid == false)
        {
            throw new ArgumentException(
                "Fixed calendar time-zone providers require a valid time-zone ID.",
                nameof(timeZoneId));
        }

        mTimeZoneId = timeZoneId;
    }

    public CalendarTimeZoneId GetTimeZoneId()
    {
        return mTimeZoneId;
    }
}
