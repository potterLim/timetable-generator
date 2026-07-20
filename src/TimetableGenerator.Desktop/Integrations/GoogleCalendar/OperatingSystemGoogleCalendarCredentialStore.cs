using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed partial class OperatingSystemGoogleCalendarCredentialStore
    : IGoogleCalendarCredentialStore
{
    private const string CREDENTIAL_SERVICE_NAME = "TimetableGenerator.GoogleCalendar";

    public Task<GoogleRefreshToken?> ReadRefreshTokenOrNullAsync(
        GoogleOAuthClientId clientId,
        CancellationToken cancellationToken)
    {
        if (clientId == null)
        {
            throw new ArgumentNullException(nameof(clientId));
        }

        return Task.Run(
            delegate
            {
                cancellationToken.ThrowIfCancellationRequested();
                string accountName = createAccountName(clientId);
                GoogleRefreshToken? refreshTokenOrNull;
                if (OperatingSystem.IsWindows())
                {
                    refreshTokenOrNull = readWindowsCredentialOrNull(accountName);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    refreshTokenOrNull = readMacOsCredentialOrNull(accountName);
                }
                else
                {
                    throw createPlatformNotSupportedException();
                }

                cancellationToken.ThrowIfCancellationRequested();
                return refreshTokenOrNull;
            },
            cancellationToken);
    }

    public Task SaveRefreshTokenAsync(
        GoogleOAuthClientId clientId,
        GoogleRefreshToken refreshToken,
        CancellationToken cancellationToken)
    {
        if (clientId == null)
        {
            throw new ArgumentNullException(nameof(clientId));
        }

        if (refreshToken == null)
        {
            throw new ArgumentNullException(nameof(refreshToken));
        }

        return Task.Run(
            delegate
            {
                cancellationToken.ThrowIfCancellationRequested();
                string accountName = createAccountName(clientId);
                if (OperatingSystem.IsWindows())
                {
                    saveWindowsCredential(accountName, refreshToken);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    saveMacOsCredential(accountName, refreshToken);
                }
                else
                {
                    throw createPlatformNotSupportedException();
                }
            },
            cancellationToken);
    }

    public Task DeleteRefreshTokenAsync(
        GoogleOAuthClientId clientId,
        CancellationToken cancellationToken)
    {
        if (clientId == null)
        {
            throw new ArgumentNullException(nameof(clientId));
        }

        return Task.Run(
            delegate
            {
                cancellationToken.ThrowIfCancellationRequested();
                string accountName = createAccountName(clientId);
                if (OperatingSystem.IsWindows())
                {
                    deleteWindowsCredential(accountName);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    deleteMacOsCredential(accountName);
                }
                else
                {
                    throw createPlatformNotSupportedException();
                }
            },
            cancellationToken);
    }

    private static string createAccountName(GoogleOAuthClientId clientId)
    {
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(clientId.Value));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static PlatformNotSupportedException createPlatformNotSupportedException()
    {
        return new PlatformNotSupportedException(
            "Secure Google Calendar credential storage is supported on Windows and macOS.");
    }
}
