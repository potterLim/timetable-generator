namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleCalendarOAuthEnvironmentValues(
    string? ClientIdOrNull,
    string? ClientSecretOrNull);
