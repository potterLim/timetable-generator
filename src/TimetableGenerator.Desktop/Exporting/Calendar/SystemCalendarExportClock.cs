using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class SystemCalendarExportClock : ICalendarExportClock
{
    public CalendarExportTimestamp GetCurrentTimestamp()
    {
        return new CalendarExportTimestamp(DateTimeOffset.UtcNow);
    }
}
