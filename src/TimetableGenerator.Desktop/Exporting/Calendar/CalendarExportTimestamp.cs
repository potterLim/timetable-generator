using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal readonly record struct CalendarExportTimestamp
{
    public DateTimeOffset UtcValue { get; }

    public CalendarExportTimestamp(DateTimeOffset value)
    {
        UtcValue = value.ToUniversalTime();
    }
}
