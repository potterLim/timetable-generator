namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal enum EAppleCalendarExportStatus
{
    None = 0,
    Success = 1,
    Cancelled = 2,
    Unavailable = 3,
    AccessDenied = 4,
    Failed = 5,
}
