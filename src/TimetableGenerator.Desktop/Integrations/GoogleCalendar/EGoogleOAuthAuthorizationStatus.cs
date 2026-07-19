namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal enum EGoogleOAuthAuthorizationStatus
{
    None = 0,
    Completed = 1,
    NotConfigured = 2,
    Cancelled = 3,
    Failed = 4,
    NetworkFailed = 5,
}
