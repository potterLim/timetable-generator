using System;

using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Tests.Exporting;

internal sealed class FixedCalendarExportClock : ICalendarExportClock
{
    private readonly CalendarExportTimestamp mTimestamp;

    public FixedCalendarExportClock(DateTimeOffset timestamp)
    {
        mTimestamp = new CalendarExportTimestamp(timestamp);
    }

    public CalendarExportTimestamp GetCurrentTimestamp()
    {
        return mTimestamp;
    }
}
