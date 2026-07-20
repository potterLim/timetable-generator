namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal enum EAppleCalendarNativeFailureKind
{
    None = 0,
    AccessDenied = 1,
    CalendarChanged = 2,
    Unavailable = 3,
    OperationFailed = 4,
}
