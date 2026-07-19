using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal interface IGoogleOAuthAuthorizationCodeProvider
{
    Task<GoogleOAuthAuthorizationCodeResult> RequestCodeAsync(
        GoogleOAuthClientId clientId,
        GoogleOAuthState state,
        GooglePkceCodeChallenge codeChallenge,
        CancellationToken cancellationToken);
}
