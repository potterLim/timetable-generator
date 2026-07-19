namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal enum EGoogleCalendarExportStatus
{
    None = 0,
    Success = 1,
    NotConfigured = 2,
    AuthenticationCancelled = 3,
    AuthenticationFailed = 4,
    AccessDenied = 5,
    NetworkFailed = 6,
    Failed = 7,
}
