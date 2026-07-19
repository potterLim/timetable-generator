using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal interface IGoogleAccessTokenProvider
{
    Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(
        CancellationToken cancellationToken);
}
