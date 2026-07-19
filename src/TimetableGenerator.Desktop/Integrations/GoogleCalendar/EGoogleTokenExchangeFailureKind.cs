namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal enum EGoogleTokenExchangeFailureKind
{
    None = 0,
    Permanent = 1,
    InvalidGrant = 2,
    Network = 3,
}
