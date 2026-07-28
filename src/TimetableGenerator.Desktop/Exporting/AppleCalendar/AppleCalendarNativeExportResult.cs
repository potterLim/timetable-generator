using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarNativeExportResult
{
    public AppleCalendarId CalendarId { get; }

    public PlanName CalendarName { get; }

    public int CreatedEventCount { get; }

    public int DeletedEventCount { get; }

    public AppleCalendarNativeExportResult(AppleCalendarId calendarId, PlanName calendarName, int createdEventCount, int deletedEventCount)
    {
        if (calendarId == null)
        {
            throw new ArgumentNullException(nameof(calendarId));
        }

        if (calendarName == null)
        {
            throw new ArgumentNullException(nameof(calendarName));
        }

        if (createdEventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(createdEventCount));
        }

        if (deletedEventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deletedEventCount));
        }

        CalendarId = calendarId;
        CalendarName = calendarName;
        CreatedEventCount = createdEventCount;
        DeletedEventCount = deletedEventCount;
    }
}
