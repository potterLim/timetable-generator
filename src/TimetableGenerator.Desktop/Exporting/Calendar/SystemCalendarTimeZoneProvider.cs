using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class SystemCalendarTimeZoneProvider : ICalendarTimeZoneProvider
{
    public CalendarTimeZoneId GetTimeZoneId()
    {
        return CalendarTimeZoneId.CreateFromSystemTimeZone(
            TimeZoneInfo.Local);
    }
}
