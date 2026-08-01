using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
{
    private sealed class ThrowingCodeProvider : IGoogleOAuthAuthorizationCodeProvider
    {
        public Task<GoogleOAuthAuthorizationCodeResult> RequestCodeAsync(GoogleOAuthClientId clientId, GoogleOAuthState state, GooglePkceCodeChallenge codeChallenge, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The browser flow must not be called.");
        }
    }

    private sealed class FailingBrowserLauncher : IExternalBrowserLauncher
    {
        public void Launch(Uri uri)
        {
            throw new Win32Exception(2);
        }
    }

    private sealed class RecordingBrowserLauncher : IExternalBrowserLauncher
    {
        public Uri? LaunchedUriOrNull { get; private set; }

        public void Launch(Uri uri)
        {
            LaunchedUriOrNull = uri;
        }
    }

    private sealed class ThrowingBrowserLauncher : IExternalBrowserLauncher
    {
        private readonly Exception mException;

        public ThrowingBrowserLauncher(Exception exception)
        {
            mException = exception;
        }

        public void Launch(Uri uri)
        {
            throw mException;
        }
    }

    private sealed class ProbeThenCallbackBrowserLauncher : IExternalBrowserLauncher
    {
        private readonly CancellationToken mCancellationToken;

        public Task CallbackTask { get; private set; } = Task.CompletedTask;

        public HttpStatusCode? ProbeStatusCodeOrNull { get; private set; }

        public HttpStatusCode? CallbackStatusCodeOrNull { get; private set; }

        public Uri? CallbackRedirectUriOrNull { get; private set; }

        public string? CallbackCacheControlOrNull { get; private set; }

        public string? CallbackReferrerPolicyOrNull { get; private set; }

        public string? CallbackBodyOrNull { get; private set; }

        public string? CallbackContentSecurityPolicyOrNull { get; private set; }

        public string? ProbeBodyOrNull { get; private set; }

        public string? ProbeContentSecurityPolicyOrNull { get; private set; }

        public ProbeThenCallbackBrowserLauncher(CancellationToken cancellationToken)
        {
            mCancellationToken = cancellationToken;
        }

        public void Launch(Uri uri)
        {
            CallbackTask = sendCallbacksAsync(uri);
        }

        private async Task sendCallbacksAsync(Uri authorizationUri)
        {
            string redirectUriValue = getQueryParameter(authorizationUri, "redirect_uri");
            Uri redirectUri = new Uri(redirectUriValue, UriKind.Absolute);
            Uri authorityUri = new Uri(redirectUri.GetLeftPart(UriPartial.Authority), UriKind.Absolute);
            using (HttpClientHandler handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
            })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage probeResponse = await client.GetAsync(new Uri(authorityUri, "/probe"), mCancellationToken))
            {
                ProbeStatusCodeOrNull = probeResponse.StatusCode;
                ProbeBodyOrNull = await probeResponse.Content.ReadAsStringAsync(mCancellationToken);
                ProbeContentSecurityPolicyOrNull = getHeaderValueOrNull(probeResponse, "Content-Security-Policy");
            }

            string callbackQuery = "?code=authorization-code&state=opaque-state";
            using (HttpClientHandler handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
            })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage callbackResponse = await client.GetAsync(new Uri(redirectUri.AbsoluteUri + callbackQuery, UriKind.Absolute), mCancellationToken))
            {
                CallbackStatusCodeOrNull = callbackResponse.StatusCode;
                CallbackRedirectUriOrNull = callbackResponse.Headers.Location;
                CallbackCacheControlOrNull = getHeaderValueOrNull(callbackResponse, "Cache-Control");
                CallbackReferrerPolicyOrNull = getHeaderValueOrNull(callbackResponse, "Referrer-Policy");
                CallbackBodyOrNull = await callbackResponse.Content.ReadAsStringAsync(mCancellationToken);
                CallbackContentSecurityPolicyOrNull = getHeaderValueOrNull(callbackResponse, "Content-Security-Policy");
            }
        }

        private static string? getHeaderValueOrNull(HttpResponseMessage response, string headerName)
        {
            IEnumerable<string>? values;
            return response.Headers.TryGetValues(headerName, out values) ? string.Join(", ", values) : null;
        }

        private static string getQueryParameter(Uri uri, string parameterName)
        {
            string query = uri.Query.StartsWith("?", StringComparison.Ordinal) ? uri.Query[1..] : uri.Query;
            foreach (string pair in query.Split('&'))
            {
                string[] parts = pair.Split('=', 2);
                if (parts.Length == 2 && string.Equals(Uri.UnescapeDataString(parts[0]), parameterName, StringComparison.Ordinal))
                {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }

            throw new InvalidOperationException("The authorization URL does not contain the expected query parameter.");
        }
    }

    private sealed class AccessDeniedCallbackBrowserLauncher : IExternalBrowserLauncher
    {
        private readonly CancellationToken mCancellationToken;

        public Task CallbackTask { get; private set; } = Task.CompletedTask;

        public HttpStatusCode? CallbackStatusCodeOrNull { get; private set; }

        public string? CallbackBodyOrNull { get; private set; }

        public string? CallbackContentSecurityPolicyOrNull { get; private set; }

        public AccessDeniedCallbackBrowserLauncher(CancellationToken cancellationToken)
        {
            mCancellationToken = cancellationToken;
        }

        public void Launch(Uri uri)
        {
            CallbackTask = sendCallbackAsync(uri);
        }

        private async Task sendCallbackAsync(Uri authorizationUri)
        {
            string redirectUriValue = getQueryParameter(authorizationUri, "redirect_uri");
            Uri redirectUri = new Uri(redirectUriValue, UriKind.Absolute);
            Uri callbackUri = new Uri(redirectUri.AbsoluteUri + "?error=access_denied&state=opaque-state", UriKind.Absolute);
            using (HttpClientHandler handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseProxy = false,
            })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage callbackResponse = await client.GetAsync(callbackUri, mCancellationToken))
            {
                CallbackStatusCodeOrNull = callbackResponse.StatusCode;
                CallbackBodyOrNull = await callbackResponse.Content.ReadAsStringAsync(mCancellationToken);
                CallbackContentSecurityPolicyOrNull = getHeaderValueOrNull(callbackResponse, "Content-Security-Policy");
            }
        }

        private static string? getHeaderValueOrNull(HttpResponseMessage response, string headerName)
        {
            IEnumerable<string>? values;
            return response.Headers.TryGetValues(headerName, out values) ? string.Join(", ", values) : null;
        }

        private static string getQueryParameter(Uri uri, string parameterName)
        {
            string query = uri.Query.StartsWith("?", StringComparison.Ordinal) ? uri.Query[1..] : uri.Query;
            foreach (string pair in query.Split('&'))
            {
                string[] parts = pair.Split('=', 2);
                if (parts.Length == 2 && string.Equals(Uri.UnescapeDataString(parts[0]), parameterName, StringComparison.Ordinal))
                {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }

            throw new InvalidOperationException("The authorization URL does not contain the expected query parameter.");
        }
    }
}
