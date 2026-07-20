using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarOAuthClient : IGoogleAccessTokenProvider
{
    private const long MAXIMUM_TOKEN_RESPONSE_BODY_BYTES = 65_536L;

    private static readonly Uri TOKEN_ENDPOINT = new Uri(
        "https://oauth2.googleapis.com/token",
        UriKind.Absolute);

    private readonly HttpClient mHttpClient;
    private readonly IGoogleCalendarOAuthConfigurationProvider mConfigurationProvider;
    private readonly IGoogleOAuthAuthorizationCodeProvider mAuthorizationCodeProvider;

    public GoogleCalendarOAuthClient(
        HttpClient httpClient,
        IGoogleCalendarOAuthConfigurationProvider configurationProvider,
        IGoogleOAuthAuthorizationCodeProvider authorizationCodeProvider)
    {
        if (httpClient == null)
        {
            throw new ArgumentNullException(nameof(httpClient));
        }

        if (configurationProvider == null)
        {
            throw new ArgumentNullException(nameof(configurationProvider));
        }

        if (authorizationCodeProvider == null)
        {
            throw new ArgumentNullException(nameof(authorizationCodeProvider));
        }

        mHttpClient = httpClient;
        mConfigurationProvider = configurationProvider;
        mAuthorizationCodeProvider = authorizationCodeProvider;
    }

    public async Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(
        CancellationToken cancellationToken)
    {
        GoogleCalendarOAuthConfiguration? configurationOrNull =
            mConfigurationProvider.GetConfigurationOrNull();
        if (configurationOrNull == null)
        {
            return GoogleOAuthAuthorizationResult.Fail(
                EGoogleOAuthAuthorizationStatus.NotConfigured,
                "oauth_client_not_configured");
        }

        try
        {
            return await authorizeInteractivelyAsync(
                configurationOrNull.ClientId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (
            cancellationToken.IsCancellationRequested == false)
        {
            Trace.TraceError(
                $"Google Calendar authorization timed out.{Environment.NewLine}{exception}");

            return GoogleOAuthAuthorizationResult.Fail(
                EGoogleOAuthAuthorizationStatus.NetworkFailed,
                "oauth_timeout");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException
            || exception is IOException)
        {
            Trace.TraceError(
                $"Google Calendar authorization transport failed.{Environment.NewLine}{exception}");

            return GoogleOAuthAuthorizationResult.Fail(
                EGoogleOAuthAuthorizationStatus.NetworkFailed,
                "oauth_transport_failed");
        }
        catch (Exception exception) when (
            exception is JsonException
            || exception is PlatformNotSupportedException
            || exception is InvalidOperationException)
        {
            Trace.TraceError(
                $"Google Calendar authorization infrastructure failed.{Environment.NewLine}{exception}");

            return GoogleOAuthAuthorizationResult.Fail(
                EGoogleOAuthAuthorizationStatus.Failed,
                "oauth_infrastructure_failed");
        }
    }

    private async Task<GoogleOAuthAuthorizationResult> authorizeInteractivelyAsync(
        GoogleOAuthClientId clientId,
        CancellationToken cancellationToken)
    {
        GoogleOAuthState state = new GoogleOAuthState(
            createRandomBase64UrlValue(32));
        GooglePkceCodeVerifier codeVerifier = new GooglePkceCodeVerifier(
            createRandomBase64UrlValue(64));
        byte[] challengeDigest = SHA256.HashData(
            Encoding.ASCII.GetBytes(codeVerifier.Value));
        GooglePkceCodeChallenge codeChallenge = new GooglePkceCodeChallenge(
            encodeBase64Url(challengeDigest));
        GoogleOAuthAuthorizationCodeResult codeResult =
            await mAuthorizationCodeProvider.RequestCodeAsync(
                clientId,
                state,
                codeChallenge,
                cancellationToken).ConfigureAwait(false);
        if (codeResult.Status != EGoogleOAuthAuthorizationStatus.Completed)
        {
            return GoogleOAuthAuthorizationResult.Fail(
                codeResult.Status,
                codeResult.DiagnosticCodeOrNull);
        }

        GoogleOAuthAuthorizationCode? authorizationCodeOrNull =
            codeResult.AuthorizationCodeOrNull;
        if (authorizationCodeOrNull == null)
        {
            return GoogleOAuthAuthorizationResult.Fail(
                EGoogleOAuthAuthorizationStatus.Failed,
                "authorization_code_missing");
        }

        GoogleTokenExchangeResult exchangeResult = await exchangeAuthorizationCodeAsync(
            clientId,
            authorizationCodeOrNull,
            codeVerifier,
            codeResult.RedirectUri,
            cancellationToken).ConfigureAwait(false);
        if (exchangeResult.AccessTokenOrNull == null)
        {
            return GoogleOAuthAuthorizationResult.Fail(
                exchangeResult.FailureKind == EGoogleTokenExchangeFailureKind.Network
                    ? EGoogleOAuthAuthorizationStatus.NetworkFailed
                    : EGoogleOAuthAuthorizationStatus.Failed,
                exchangeResult.DiagnosticCodeOrNull);
        }

        return GoogleOAuthAuthorizationResult.Complete(exchangeResult.AccessTokenOrNull);
    }

    private async Task<GoogleTokenExchangeResult> exchangeAuthorizationCodeAsync(
        GoogleOAuthClientId clientId,
        GoogleOAuthAuthorizationCode authorizationCode,
        GooglePkceCodeVerifier codeVerifier,
        GoogleOAuthRedirectUri redirectUri,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId.Value,
            ["code"] = authorizationCode.Value,
            ["code_verifier"] = codeVerifier.Value,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri.Value.AbsoluteUri,
        };
        return await sendTokenRequestAsync(parameters, cancellationToken).ConfigureAwait(false);
    }

    private async Task<GoogleTokenExchangeResult> sendTokenRequestAsync(
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        using (FormUrlEncodedContent content = new FormUrlEncodedContent(parameters))
        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, TOKEN_ENDPOINT)
        {
            Content = content,
        })
        using (HttpResponseMessage response = await mHttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false))
        {
            byte[] responseContent;
            try
            {
                responseContent = await GoogleHttpResponseBodyReader.ReadAsync(
                    response.Content,
                    MAXIMUM_TOKEN_RESPONSE_BODY_BYTES,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (GoogleHttpResponseBodyLimitExceededException)
            {
                return GoogleTokenExchangeResult.Fail(
                    EGoogleTokenExchangeFailureKind.Permanent,
                    "oauth_response_too_large");
            }

            if (response.IsSuccessStatusCode == false)
            {
                string? errorOrNull = getErrorOrNull(responseContent);
                string diagnosticCode = errorOrNull == null
                    ? createHttpDiagnosticCode(response.StatusCode)
                    : errorOrNull;
                int numericStatusCode = (int)response.StatusCode;
                bool isNetworkFailure = response.StatusCode == HttpStatusCode.RequestTimeout
                    || response.StatusCode == HttpStatusCode.TooManyRequests
                    || numericStatusCode >= 500;
                EGoogleTokenExchangeFailureKind failureKind;
                if (isNetworkFailure)
                {
                    failureKind = EGoogleTokenExchangeFailureKind.Network;
                }
                else
                {
                    failureKind = EGoogleTokenExchangeFailureKind.Permanent;
                }

                return GoogleTokenExchangeResult.Fail(
                    failureKind,
                    diagnosticCode);
            }

            using (JsonDocument document = JsonDocument.Parse(responseContent))
            {
                string? accessTokenOrNull = getStringOrNull(
                    document.RootElement,
                    "access_token");
                if (string.IsNullOrWhiteSpace(accessTokenOrNull))
                {
                    return GoogleTokenExchangeResult.Fail(
                        EGoogleTokenExchangeFailureKind.Permanent,
                        "access_token_missing");
                }

                string? tokenTypeOrNull = getStringOrNull(
                    document.RootElement,
                    "token_type");
                if (string.Equals(
                    tokenTypeOrNull,
                    "Bearer",
                    StringComparison.OrdinalIgnoreCase) == false)
                {
                    return GoogleTokenExchangeResult.Fail(
                        EGoogleTokenExchangeFailureKind.Permanent,
                        "unsupported_token_type");
                }

                return GoogleTokenExchangeResult.Complete(
                    new GoogleAccessToken(accessTokenOrNull));
            }
        }
    }

    private static string createRandomBase64UrlValue(int byteCount)
    {
        byte[] value = RandomNumberGenerator.GetBytes(byteCount);
        return encodeBase64Url(value);
    }

    private static string encodeBase64Url(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string? getStringOrNull(JsonElement element, string propertyName)
    {
        JsonElement property;
        if (element.ValueKind != JsonValueKind.Object
            || element.TryGetProperty(propertyName, out property) == false
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static string? getErrorOrNull(byte[] responseContent)
    {
        try
        {
            using (JsonDocument document = JsonDocument.Parse(responseContent))
            {
                return getStringOrNull(document.RootElement, "error");
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string createHttpDiagnosticCode(HttpStatusCode statusCode)
    {
        return "oauth_http_" + ((int)statusCode).ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }

}
