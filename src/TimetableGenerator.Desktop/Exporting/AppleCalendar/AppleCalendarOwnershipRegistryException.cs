using System;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarOwnershipRegistryException : Exception
{
    public AppleCalendarOwnershipRegistryException(string message)
        : base(message)
    {
    }

    public AppleCalendarOwnershipRegistryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
