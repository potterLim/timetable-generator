using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleTokenExchangeResult
{
    public GoogleAccessToken? AccessTokenOrNull { get; }

    public EGoogleTokenExchangeFailureKind FailureKind { get; }

    public string? DiagnosticCodeOrNull { get; }

    private GoogleTokenExchangeResult(
        GoogleAccessToken? accessTokenOrNull,
        EGoogleTokenExchangeFailureKind failureKind,
        string? diagnosticCodeOrNull)
    {
        if (Enum.IsDefined(typeof(EGoogleTokenExchangeFailureKind), failureKind) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind));
        }

        bool isCompleted = accessTokenOrNull != null;
        if (isCompleted != (failureKind == EGoogleTokenExchangeFailureKind.None))
        {
            throw new ArgumentException(
                "Token exchange completion and failure state do not match.",
                nameof(failureKind));
        }

        AccessTokenOrNull = accessTokenOrNull;
        FailureKind = failureKind;
        DiagnosticCodeOrNull = diagnosticCodeOrNull;
    }

    public static GoogleTokenExchangeResult Complete(GoogleAccessToken accessToken)
    {
        return new GoogleTokenExchangeResult(
            accessToken,
            EGoogleTokenExchangeFailureKind.None,
            null);
    }

    public static GoogleTokenExchangeResult Fail(
        EGoogleTokenExchangeFailureKind failureKind,
        string diagnosticCode)
    {
        if (failureKind == EGoogleTokenExchangeFailureKind.None)
        {
            throw new ArgumentException(
                "Failed token exchanges require a failure kind.",
                nameof(failureKind));
        }

        if (string.IsNullOrWhiteSpace(diagnosticCode))
        {
            throw new ArgumentException(
                "Failed token exchanges require a diagnostic code.",
                nameof(diagnosticCode));
        }

        return new GoogleTokenExchangeResult(
            null,
            failureKind,
            diagnosticCode);
    }
}
