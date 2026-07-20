using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class LoopbackGoogleOAuthAuthorizationCodeProvider
    : IGoogleOAuthAuthorizationCodeProvider
{
    private const int MAXIMUM_REQUEST_HEADER_BYTES = 16_384;
    private const int LISTENER_BACKLOG = 8;
    private const string CALLBACK_PATH = "/oauth2/callback";
    private const string GOOGLE_CALENDAR_SCOPES =
        "https://www.googleapis.com/auth/calendar.app.created "
        + "https://www.googleapis.com/auth/calendar.calendarlist.readonly";

    private static readonly Uri AUTHORIZATION_ENDPOINT = new Uri(
        "https://accounts.google.com/o/oauth2/v2/auth",
        UriKind.Absolute);

    private static readonly TimeSpan CONNECTION_TIMEOUT = TimeSpan.FromSeconds(5.0);

    private readonly IExternalBrowserLauncher mBrowserLauncher;
    private readonly TimeSpan mAuthorizationTimeout;

    public LoopbackGoogleOAuthAuthorizationCodeProvider(
        IExternalBrowserLauncher browserLauncher)
        : this(browserLauncher, TimeSpan.FromMinutes(5.0))
    {
    }

    internal LoopbackGoogleOAuthAuthorizationCodeProvider(
        IExternalBrowserLauncher browserLauncher,
        TimeSpan authorizationTimeout)
    {
        if (browserLauncher == null)
        {
            throw new ArgumentNullException(nameof(browserLauncher));
        }

        if (authorizationTimeout <= TimeSpan.Zero
            || authorizationTimeout > TimeSpan.FromMinutes(10.0))
        {
            throw new ArgumentOutOfRangeException(nameof(authorizationTimeout));
        }

        mBrowserLauncher = browserLauncher;
        mAuthorizationTimeout = authorizationTimeout;
    }

    public async Task<GoogleOAuthAuthorizationCodeResult> RequestCodeAsync(
        GoogleOAuthClientId clientId,
        GoogleOAuthState state,
        GooglePkceCodeChallenge codeChallenge,
        CancellationToken cancellationToken)
    {
        if (clientId == null)
        {
            throw new ArgumentNullException(nameof(clientId));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (codeChallenge == null)
        {
            throw new ArgumentNullException(nameof(codeChallenge));
        }

        TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        using (CancellationTokenSource timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeoutSource.CancelAfter(mAuthorizationTimeout);
            listener.Start(LISTENER_BACKLOG);
            try
            {
                IPEndPoint listenerEndpoint = (IPEndPoint)listener.LocalEndpoint;
                GoogleOAuthRedirectUri redirectUri = new GoogleOAuthRedirectUri(
                    new Uri(
                        "http://127.0.0.1:"
                        + listenerEndpoint.Port.ToString(CultureInfo.InvariantCulture)
                        + CALLBACK_PATH,
                        UriKind.Absolute));
                Uri authorizationUri = createAuthorizationUri(
                    clientId,
                    redirectUri,
                    state,
                    codeChallenge);
                try
                {
                    mBrowserLauncher.Launch(authorizationUri);
                }
                catch (Exception exception) when (
                    exception is Win32Exception
                    || exception is InvalidOperationException
                    || exception is PlatformNotSupportedException)
                {
                    return GoogleOAuthAuthorizationCodeResult.Fail(
                        EGoogleOAuthAuthorizationStatus.Failed,
                        redirectUri,
                        "browser_launch_failed");
                }

                while (true)
                {
                    using (TcpClient client = await listener.AcceptTcpClientAsync(
                        timeoutSource.Token).ConfigureAwait(false))
                    using (NetworkStream stream = client.GetStream())
                    using (CancellationTokenSource connectionSource =
                        CancellationTokenSource.CreateLinkedTokenSource(
                            timeoutSource.Token))
                    {
                        connectionSource.CancelAfter(CONNECTION_TIMEOUT);
                        try
                        {
                            string requestLine = await readRequestLineAsync(
                                stream,
                                connectionSource.Token).ConfigureAwait(false);
                            GoogleOAuthAuthorizationCodeResult result = parseRequestLine(
                                requestLine,
                                redirectUri,
                                state);
                            bool shouldContinue = isIgnorableCallbackFailure(result);
                            EGoogleLoopbackResponseKind responseKind = shouldContinue
                                ? EGoogleLoopbackResponseKind.InvalidRequest
                                : result.Status == EGoogleOAuthAuthorizationStatus.Completed
                                    ? EGoogleLoopbackResponseKind.Success
                                    : EGoogleLoopbackResponseKind.AuthorizationFailed;
                            try
                            {
                                await writeBrowserResponseAsync(
                                    stream,
                                    responseKind,
                                    connectionSource.Token).ConfigureAwait(false);
                            }
                            catch (Exception exception) when (
                                exception is IOException
                                || exception is SocketException)
                            {
                            }
                            catch (OperationCanceledException) when (
                                cancellationToken.IsCancellationRequested == false)
                            {
                            }

                            if (shouldContinue)
                            {
                                continue;
                            }

                            cancellationToken.ThrowIfCancellationRequested();
                            return result;
                        }
                        catch (OperationCanceledException) when (
                            timeoutSource.IsCancellationRequested == false)
                        {
                            continue;
                        }
                        catch (Exception exception) when (
                            exception is IOException
                            || exception is SocketException
                            || exception is UriFormatException)
                        {
                            continue;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested == false)
            {
                GoogleOAuthRedirectUri timeoutRedirectUri = createFallbackRedirectUri();
                return GoogleOAuthAuthorizationCodeResult.Fail(
                    EGoogleOAuthAuthorizationStatus.Failed,
                    timeoutRedirectUri,
                    "authorization_timeout");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is SocketException
                || exception is UriFormatException)
            {
                GoogleOAuthRedirectUri fallbackRedirectUri = createFallbackRedirectUri();
                return GoogleOAuthAuthorizationCodeResult.Fail(
                    EGoogleOAuthAuthorizationStatus.Failed,
                    fallbackRedirectUri,
                    "loopback_transport_failed");
            }
            finally
            {
                listener.Stop();
            }
        }
    }

    internal static Uri createAuthorizationUri(
        GoogleOAuthClientId clientId,
        GoogleOAuthRedirectUri redirectUri,
        GoogleOAuthState state,
        GooglePkceCodeChallenge codeChallenge)
    {
        Dictionary<string, string> parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId.Value,
            ["redirect_uri"] = redirectUri.Value.AbsoluteUri,
            ["response_type"] = "code",
            ["scope"] = GOOGLE_CALENDAR_SCOPES,
            ["code_challenge"] = codeChallenge.Value,
            ["code_challenge_method"] = "S256",
            ["state"] = state.Value,
        };

        StringBuilder query = new StringBuilder();
        foreach (KeyValuePair<string, string> parameter in parameters)
        {
            if (query.Length > 0)
            {
                query.Append('&');
            }

            query.Append(Uri.EscapeDataString(parameter.Key));
            query.Append('=');
            query.Append(Uri.EscapeDataString(parameter.Value));
        }

        return new Uri(AUTHORIZATION_ENDPOINT.AbsoluteUri + "?" + query, UriKind.Absolute);
    }

    internal static GoogleOAuthAuthorizationCodeResult parseRequestLine(
        string requestLine,
        GoogleOAuthRedirectUri redirectUri,
        GoogleOAuthState expectedState)
    {
        string[] parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || string.Equals(parts[0], "GET", StringComparison.Ordinal) == false)
        {
            return GoogleOAuthAuthorizationCodeResult.Fail(
                EGoogleOAuthAuthorizationStatus.Failed,
                redirectUri,
                "invalid_loopback_request");
        }

        Uri requestUri = new Uri("http://127.0.0.1" + parts[1], UriKind.Absolute);
        if (string.Equals(requestUri.AbsolutePath, CALLBACK_PATH, StringComparison.Ordinal) == false)
        {
            return GoogleOAuthAuthorizationCodeResult.Fail(
                EGoogleOAuthAuthorizationStatus.Failed,
                redirectUri,
                "invalid_callback_path");
        }

        IReadOnlyDictionary<string, string> query = parseQuery(requestUri.Query);
        string? returnedState;
        if (query.TryGetValue("state", out returnedState) == false
            || fixedTimeEquals(returnedState, expectedState.Value) == false)
        {
            return GoogleOAuthAuthorizationCodeResult.Fail(
                EGoogleOAuthAuthorizationStatus.Failed,
                redirectUri,
                "oauth_state_mismatch");
        }

        string? error;
        if (query.TryGetValue("error", out error))
        {
            EGoogleOAuthAuthorizationStatus status =
                string.Equals(error, "access_denied", StringComparison.Ordinal)
                    ? EGoogleOAuthAuthorizationStatus.Cancelled
                    : EGoogleOAuthAuthorizationStatus.Failed;
            return GoogleOAuthAuthorizationCodeResult.Fail(status, redirectUri, error);
        }

        string? code;
        if (query.TryGetValue("code", out code) == false
            || string.IsNullOrWhiteSpace(code))
        {
            return GoogleOAuthAuthorizationCodeResult.Fail(
                EGoogleOAuthAuthorizationStatus.Failed,
                redirectUri,
                "authorization_code_missing");
        }

        return GoogleOAuthAuthorizationCodeResult.Complete(
            new GoogleOAuthAuthorizationCode(code),
            redirectUri);
    }

    private static IReadOnlyDictionary<string, string> parseQuery(string query)
    {
        Dictionary<string, string> values = new Dictionary<string, string>(
            StringComparer.Ordinal);
        string queryWithoutPrefix = query.StartsWith("?", StringComparison.Ordinal)
            ? query[1..]
            : query;
        foreach (string part in queryWithoutPrefix.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int separatorIndex = part.IndexOf('=', StringComparison.Ordinal);
            string encodedName = separatorIndex < 0 ? part : part[..separatorIndex];
            string encodedValue = separatorIndex < 0 ? string.Empty : part[(separatorIndex + 1)..];
            string name = Uri.UnescapeDataString(encodedName.Replace('+', ' '));
            string value = Uri.UnescapeDataString(encodedValue.Replace('+', ' '));
            values[name] = value;
        }

        return values;
    }

    private static bool fixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static async Task<string> readRequestLineAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[MAXIMUM_REQUEST_HEADER_BYTES];
        int length = 0;
        while (length < buffer.Length)
        {
            int bytesRead = await stream.ReadAsync(
                buffer.AsMemory(length, buffer.Length - length),
                cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                throw new IOException(
                    "The Google OAuth loopback request ended before its headers.");
            }

            length += bytesRead;
            if (findHeaderTerminatorIndex(buffer, length) >= 0)
            {
                int requestLineEndIndex = findRequestLineEndIndex(buffer, length);
                if (requestLineEndIndex <= 0)
                {
                    throw new IOException(
                        "The Google OAuth loopback request line is invalid.");
                }

                return Encoding.ASCII.GetString(buffer, 0, requestLineEndIndex);
            }
        }

        throw new IOException("The Google OAuth loopback request header is invalid.");
    }

    private static async Task writeBrowserResponseAsync(
        Stream stream,
        EGoogleLoopbackResponseKind responseKind,
        CancellationToken cancellationToken)
    {
        if (Enum.IsDefined(typeof(EGoogleLoopbackResponseKind), responseKind) == false
            || responseKind == EGoogleLoopbackResponseKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(responseKind));
        }

        string title = responseKind == EGoogleLoopbackResponseKind.Success
            ? "연결 완료"
            : "연결하지 못했습니다";
        string message = responseKind == EGoogleLoopbackResponseKind.Success
            ? "시간표 앱으로 돌아가도 됩니다."
            : responseKind == EGoogleLoopbackResponseKind.InvalidRequest
                ? "올바른 Google 로그인 응답을 기다리고 있습니다."
                : "시간표 앱으로 돌아가 다시 시도해 주세요.";
        string body = "<!doctype html><html lang=\"ko\"><head><meta charset=\"utf-8\">"
            + "<meta name=\"viewport\" content=\"width=device-width\">"
            + "<title>" + title + "</title></head><body><main><h1>"
            + title + "</h1><p>" + message + "</p></main></body></html>";
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string statusLine = responseKind == EGoogleLoopbackResponseKind.InvalidRequest
            ? "HTTP/1.1 400 Bad Request\r\n"
            : "HTTP/1.1 200 OK\r\n";
        string header = statusLine
            + "Content-Type: text/html; charset=utf-8\r\n"
            + "Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'\r\n"
            + "Cache-Control: no-store\r\n"
            + "Referrer-Policy: no-referrer\r\n"
            + "X-Content-Type-Options: nosniff\r\n"
            + "Connection: close\r\n"
            + "Content-Length: "
            + bodyBytes.Length.ToString(CultureInfo.InvariantCulture)
            + "\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static int findHeaderTerminatorIndex(byte[] buffer, int length)
    {
        for (int index = 3; index < length; ++index)
        {
            if (buffer[index - 3] == '\r'
                && buffer[index - 2] == '\n'
                && buffer[index - 1] == '\r'
                && buffer[index] == '\n')
            {
                return index - 3;
            }
        }

        return -1;
    }

    private static int findRequestLineEndIndex(byte[] buffer, int length)
    {
        for (int index = 1; index < length; ++index)
        {
            if (buffer[index - 1] == '\r' && buffer[index] == '\n')
            {
                return index - 1;
            }
        }

        return -1;
    }

    private static bool isIgnorableCallbackFailure(
        GoogleOAuthAuthorizationCodeResult result)
    {
        if (result.Status != EGoogleOAuthAuthorizationStatus.Failed)
        {
            return false;
        }

        string? diagnosticCodeOrNull = result.DiagnosticCodeOrNull;
        return string.Equals(
            diagnosticCodeOrNull,
            "invalid_loopback_request",
            StringComparison.Ordinal)
            || string.Equals(
                diagnosticCodeOrNull,
                "invalid_callback_path",
                StringComparison.Ordinal)
            || string.Equals(
                diagnosticCodeOrNull,
                "oauth_state_mismatch",
                StringComparison.Ordinal)
            || string.Equals(
                diagnosticCodeOrNull,
                "authorization_code_missing",
                StringComparison.Ordinal);
    }

    private static GoogleOAuthRedirectUri createFallbackRedirectUri()
    {
        return new GoogleOAuthRedirectUri(
            new Uri(
                "http://127.0.0.1:1" + CALLBACK_PATH,
                UriKind.Absolute));
    }
}
