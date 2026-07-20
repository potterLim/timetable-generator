namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal enum EGoogleCalendarAccessRole
{
    None = 0,
    FreeBusyReader = 1,
    Reader = 2,
    Writer = 3,
    WriterWithoutPrivateAccess = 4,
    Owner = 5,
}
