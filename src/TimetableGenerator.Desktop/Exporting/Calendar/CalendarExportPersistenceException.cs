using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class CalendarExportPersistenceException : Exception
{
    public CalendarExportPersistenceException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
