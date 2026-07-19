namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal interface ICalendarExportClock
{
    CalendarExportTimestamp GetCurrentTimestamp();
}
