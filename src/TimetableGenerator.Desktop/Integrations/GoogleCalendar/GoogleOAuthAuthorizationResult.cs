using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleOAuthAuthorizationResult
{
    public EGoogleOAuthAuthorizationStatus Status { get; }

    public GoogleAccessToken? AccessTokenOrNull { get; }

    public string? DiagnosticCodeOrNull { get; }

    private GoogleOAuthAuthorizationResult(
        EGoogleOAuthAuthorizationStatus status,
        GoogleAccessToken? accessTokenOrNull,
        string? diagnosticCodeOrNull)
    {
        if (Enum.IsDefined(typeof(EGoogleOAuthAuthorizationStatus), status) == false
            || status == EGoogleOAuthAuthorizationStatus.None)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Status = status;
        AccessTokenOrNull = accessTokenOrNull;
        DiagnosticCodeOrNull = diagnosticCodeOrNull;
    }

    public static GoogleOAuthAuthorizationResult Complete(GoogleAccessToken accessToken)
    {
        if (accessToken == null)
        {
            throw new ArgumentNullException(nameof(accessToken));
        }

        return new GoogleOAuthAuthorizationResult(
            EGoogleOAuthAuthorizationStatus.Completed,
            accessToken,
            null);
    }

    public static GoogleOAuthAuthorizationResult Fail(
        EGoogleOAuthAuthorizationStatus status,
        string? diagnosticCodeOrNull)
    {
        if (status == EGoogleOAuthAuthorizationStatus.Completed)
        {
            throw new ArgumentException(
                "Completed Google OAuth results require an access token.",
                nameof(status));
        }

        return new GoogleOAuthAuthorizationResult(status, null, diagnosticCodeOrNull);
    }
}
