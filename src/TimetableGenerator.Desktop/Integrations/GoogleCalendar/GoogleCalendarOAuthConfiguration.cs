using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarOAuthConfiguration
{
    public GoogleOAuthClientId ClientId { get; }

    public GoogleOAuthClientSecret? ClientSecretOrNull { get; }

    public GoogleCalendarOAuthConfiguration(
        GoogleOAuthClientId clientId,
        GoogleOAuthClientSecret? clientSecretOrNull = null)
    {
        if (clientId == null)
        {
            throw new ArgumentNullException(nameof(clientId));
        }

        ClientId = clientId;
        ClientSecretOrNull = clientSecretOrNull;
    }
}
