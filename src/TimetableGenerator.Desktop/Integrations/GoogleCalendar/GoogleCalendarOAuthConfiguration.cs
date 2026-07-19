using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarOAuthConfiguration
{
    public GoogleOAuthClientId ClientId { get; }

    public GoogleCalendarOAuthConfiguration(GoogleOAuthClientId clientId)
    {
        if (clientId == null)
        {
            throw new ArgumentNullException(nameof(clientId));
        }

        ClientId = clientId;
    }
}
