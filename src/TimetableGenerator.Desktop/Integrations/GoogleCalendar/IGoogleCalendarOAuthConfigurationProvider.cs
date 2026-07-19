namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal interface IGoogleCalendarOAuthConfigurationProvider
{
    GoogleCalendarOAuthConfiguration? GetConfigurationOrNull();
}
