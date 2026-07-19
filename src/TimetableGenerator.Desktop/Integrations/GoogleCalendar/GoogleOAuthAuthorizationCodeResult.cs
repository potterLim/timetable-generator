using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleOAuthAuthorizationCodeResult
{
    public EGoogleOAuthAuthorizationStatus Status { get; }

    public GoogleOAuthAuthorizationCode? AuthorizationCodeOrNull { get; }

    public GoogleOAuthRedirectUri RedirectUri { get; }

    public string? DiagnosticCodeOrNull { get; }

    private GoogleOAuthAuthorizationCodeResult(
        EGoogleOAuthAuthorizationStatus status,
        GoogleOAuthAuthorizationCode? authorizationCodeOrNull,
        GoogleOAuthRedirectUri redirectUri,
        string? diagnosticCodeOrNull)
    {
        if (redirectUri == null)
        {
            throw new ArgumentNullException(nameof(redirectUri));
        }

        if (Enum.IsDefined(typeof(EGoogleOAuthAuthorizationStatus), status) == false
            || status == EGoogleOAuthAuthorizationStatus.None)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        Status = status;
        AuthorizationCodeOrNull = authorizationCodeOrNull;
        RedirectUri = redirectUri;
        DiagnosticCodeOrNull = diagnosticCodeOrNull;
    }

    public static GoogleOAuthAuthorizationCodeResult Complete(
        GoogleOAuthAuthorizationCode authorizationCode,
        GoogleOAuthRedirectUri redirectUri)
    {
        if (authorizationCode == null)
        {
            throw new ArgumentNullException(nameof(authorizationCode));
        }

        return new GoogleOAuthAuthorizationCodeResult(
            EGoogleOAuthAuthorizationStatus.Completed,
            authorizationCode,
            redirectUri,
            null);
    }

    public static GoogleOAuthAuthorizationCodeResult Fail(
        EGoogleOAuthAuthorizationStatus status,
        GoogleOAuthRedirectUri redirectUri,
        string? diagnosticCodeOrNull)
    {
        if (status == EGoogleOAuthAuthorizationStatus.Completed
            || status == EGoogleOAuthAuthorizationStatus.NotConfigured)
        {
            throw new ArgumentException(
                "Authorization-code failures require a terminal browser-flow status.",
                nameof(status));
        }

        return new GoogleOAuthAuthorizationCodeResult(
            status,
            null,
            redirectUri,
            diagnosticCodeOrNull);
    }
}
