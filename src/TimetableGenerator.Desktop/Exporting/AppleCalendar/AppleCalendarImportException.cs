using System;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarImportException : Exception
{
    public AppleCalendarImportException(string message)
        : base(message)
    {
    }

    public AppleCalendarImportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
