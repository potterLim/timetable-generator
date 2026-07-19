using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal interface IGoogleCalendarCredentialStore
{
    Task<GoogleRefreshToken?> ReadRefreshTokenOrNullAsync(
        GoogleOAuthClientId clientId,
        CancellationToken cancellationToken);

    Task SaveRefreshTokenAsync(
        GoogleOAuthClientId clientId,
        GoogleRefreshToken refreshToken,
        CancellationToken cancellationToken);

    Task DeleteRefreshTokenAsync(
        GoogleOAuthClientId clientId,
        CancellationToken cancellationToken);
}
