using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed class GoogleCalendarOAuthTests
{
    [Fact]
    public async Task MissingClientConfigurationDoesNotTouchCredentialsBrowserOrNetworkAsync()
    {
        ThrowingHttpMessageHandler httpHandler = new ThrowingHttpMessageHandler();
        GoogleCalendarOAuthClient client = new GoogleCalendarOAuthClient(
            new HttpClient(httpHandler),
            new FixedConfigurationProvider(null),
            new ThrowingCredentialStore(),
            new ThrowingCodeProvider());

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(
            CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.NotConfigured, result.Status);
        Assert.Equal("oauth_client_not_configured", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task InteractiveAuthorizationUsesPkceAndStoresOnlyRefreshTokenAsync()
    {
        RecordingCredentialStore credentialStore = new RecordingCredentialStore(null);
        RecordingCodeProvider codeProvider = new RecordingCodeProvider();
        PkceTokenHttpMessageHandler httpHandler = new PkceTokenHttpMessageHandler(
            codeProvider);
        GoogleCalendarOAuthClient client = new GoogleCalendarOAuthClient(
            new HttpClient(httpHandler),
            new FixedConfigurationProvider(
                new GoogleCalendarOAuthConfiguration(
                    new GoogleOAuthClientId("client.apps.googleusercontent.com"))),
            credentialStore,
            codeProvider);

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(
            CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Completed, result.Status);
        Assert.Equal("[redacted]", result.AccessTokenOrNull?.ToString());
        Assert.Equal("saved-refresh-token", credentialStore.SavedTokenOrNull?.Value);
        Assert.True(httpHandler.PkceVerified);
        Assert.NotEqual(
            codeProvider.StateOrNull?.Value,
            codeProvider.CodeChallengeOrNull?.Value);
    }

    [Fact]
    public void AuthorizationUrlUsesDesktopLoopbackAndLeastPrivilegeScope()
    {
        Uri redirectUri = new Uri(
            "http://127.0.0.1:53122/oauth2/callback",
            UriKind.Absolute);

        Uri authorizationUri =
            LoopbackGoogleOAuthAuthorizationCodeProvider.createAuthorizationUri(
                new GoogleOAuthClientId("client.apps.googleusercontent.com"),
                new GoogleOAuthRedirectUri(redirectUri),
                new GoogleOAuthState("state-value"),
                new GooglePkceCodeChallenge("challenge-value"));

        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A53122", authorizationUri.Query);
        Assert.Contains("code_challenge_method=S256", authorizationUri.Query);
        Assert.Contains("calendar.app.created", Uri.UnescapeDataString(authorizationUri.Query));
        Assert.Contains(
            "calendar.calendarlist.readonly",
            Uri.UnescapeDataString(authorizationUri.Query));
        Assert.DoesNotContain("client_secret", authorizationUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void OAuthStateDoesNotExposeItsValueWhenFormatted()
    {
        GoogleOAuthState state = new GoogleOAuthState("state-secret");

        Assert.Equal("[redacted]", state.ToString());
    }

    [Fact]
    public void CallbackRejectsMismatchedState()
    {
        Uri redirectUri = new Uri(
            "http://127.0.0.1:53122/oauth2/callback",
            UriKind.Absolute);

        GoogleOAuthAuthorizationCodeResult result =
            LoopbackGoogleOAuthAuthorizationCodeProvider.parseRequestLine(
                "GET /oauth2/callback?code=secret-code&state=wrong HTTP/1.1",
                new GoogleOAuthRedirectUri(redirectUri),
                new GoogleOAuthState("expected"));

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("oauth_state_mismatch", result.DiagnosticCodeOrNull);
        Assert.DoesNotContain("secret-code", result.DiagnosticCodeOrNull, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowserLaunchFailureReturnsSanitizedFailureAsync()
    {
        LoopbackGoogleOAuthAuthorizationCodeProvider provider =
            new LoopbackGoogleOAuthAuthorizationCodeProvider(
                new FailingBrowserLauncher(),
                TimeSpan.FromSeconds(1.0));

        GoogleOAuthAuthorizationCodeResult result = await provider.RequestCodeAsync(
            new GoogleOAuthClientId("client.apps.googleusercontent.com"),
            new GoogleOAuthState("opaque-state"),
            new GooglePkceCodeChallenge("opaque-challenge"),
            CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("browser_launch_failed", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public void DefaultBrowserLauncherRejectsMissingOperatingSystemProcess()
    {
        DefaultExternalBrowserLauncher launcher = new DefaultExternalBrowserLauncher(
            delegate (ProcessStartInfo startInfo)
            {
                return null;
            });

        Assert.Throws<InvalidOperationException>(
            delegate
            {
                launcher.Launch(new Uri("https://accounts.google.com", UriKind.Absolute));
            });
    }

    [Fact]
    public async Task MissingOperatingSystemBrowserProcessReturnsImmediatelyAsAuthFailureAsync()
    {
        DefaultExternalBrowserLauncher launcher = new DefaultExternalBrowserLauncher(
            delegate (ProcessStartInfo startInfo)
            {
                return null;
            });
        LoopbackGoogleOAuthAuthorizationCodeProvider provider =
            new LoopbackGoogleOAuthAuthorizationCodeProvider(
                launcher,
                TimeSpan.FromMinutes(5.0));

        GoogleOAuthAuthorizationCodeResult result = await provider.RequestCodeAsync(
            new GoogleOAuthClientId("client.apps.googleusercontent.com"),
            new GoogleOAuthState("opaque-state"),
            new GooglePkceCodeChallenge("opaque-challenge"),
            CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("browser_launch_failed", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task TokenEndpointTimeoutIsReportedAsNetworkFailureAsync()
    {
        GoogleCalendarOAuthClient client = createRefreshClient(
            new TimeoutHttpMessageHandler());

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(
            CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.NetworkFailed, result.Status);
        Assert.Equal("oauth_timeout", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task TokenEndpointServiceFailureIsReportedAsNetworkFailureAsync()
    {
        GoogleCalendarOAuthClient client = createRefreshClient(
            new ServiceUnavailableHttpMessageHandler());

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(
            CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.NetworkFailed, result.Status);
        Assert.Equal("temporarily_unavailable", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task CredentialManagerFailureIsReportedAsInfrastructureFailureAsync()
    {
        GoogleCalendarOAuthClient client = new GoogleCalendarOAuthClient(
            new HttpClient(new ThrowingHttpMessageHandler()),
            new FixedConfigurationProvider(
                new GoogleCalendarOAuthConfiguration(
                    new GoogleOAuthClientId("client.apps.googleusercontent.com"))),
            new FailingCredentialStore(),
            new ThrowingCodeProvider());

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(
            CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("oauth_infrastructure_failed", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task LoopbackWaitHonorsProductTimeoutAsync()
    {
        LoopbackGoogleOAuthAuthorizationCodeProvider provider =
            new LoopbackGoogleOAuthAuthorizationCodeProvider(
                new RecordingBrowserLauncher(),
                TimeSpan.FromMilliseconds(25.0));

        GoogleOAuthAuthorizationCodeResult result = await provider.RequestCodeAsync(
            new GoogleOAuthClientId("client.apps.googleusercontent.com"),
            new GoogleOAuthState("opaque-state"),
            new GooglePkceCodeChallenge("opaque-challenge"),
            CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("authorization_timeout", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task LoopbackIgnoresProbeAndAcceptsFollowingGoogleCallbackAsync()
    {
        ProbeThenCallbackBrowserLauncher launcher =
            new ProbeThenCallbackBrowserLauncher(
                TestContext.Current.CancellationToken);
        LoopbackGoogleOAuthAuthorizationCodeProvider provider =
            new LoopbackGoogleOAuthAuthorizationCodeProvider(
                launcher,
                TimeSpan.FromSeconds(3.0));

        GoogleOAuthAuthorizationCodeResult result = await provider.RequestCodeAsync(
            new GoogleOAuthClientId("client.apps.googleusercontent.com"),
            new GoogleOAuthState("opaque-state"),
            new GooglePkceCodeChallenge("opaque-challenge"),
            TestContext.Current.CancellationToken);
        await launcher.CallbackTask.WaitAsync(
            TimeSpan.FromSeconds(2.0),
            TestContext.Current.CancellationToken);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Completed, result.Status);
        Assert.Equal("authorization-code", result.AuthorizationCodeOrNull?.Value);
        Assert.Equal(HttpStatusCode.BadRequest, launcher.ProbeStatusCodeOrNull);
        Assert.Equal(HttpStatusCode.OK, launcher.CallbackStatusCodeOrNull);
    }

    private static GoogleCalendarOAuthClient createRefreshClient(
        HttpMessageHandler handler)
    {
        return new GoogleCalendarOAuthClient(
            new HttpClient(handler),
            new FixedConfigurationProvider(
                new GoogleCalendarOAuthConfiguration(
                    new GoogleOAuthClientId("client.apps.googleusercontent.com"))),
            new RecordingCredentialStore(new GoogleRefreshToken("refresh-secret")),
            new ThrowingCodeProvider());
    }

    private sealed class FixedConfigurationProvider
        : IGoogleCalendarOAuthConfigurationProvider
    {
        private readonly GoogleCalendarOAuthConfiguration? mConfigurationOrNull;

        public FixedConfigurationProvider(
            GoogleCalendarOAuthConfiguration? configurationOrNull)
        {
            mConfigurationOrNull = configurationOrNull;
        }

        public GoogleCalendarOAuthConfiguration? GetConfigurationOrNull()
        {
            return mConfigurationOrNull;
        }
    }

    private sealed class RecordingCredentialStore : IGoogleCalendarCredentialStore
    {
        private readonly GoogleRefreshToken? mInitialTokenOrNull;

        public GoogleRefreshToken? SavedTokenOrNull { get; private set; }

        public RecordingCredentialStore(GoogleRefreshToken? initialTokenOrNull)
        {
            mInitialTokenOrNull = initialTokenOrNull;
        }

        public Task<GoogleRefreshToken?> ReadRefreshTokenOrNullAsync(
            GoogleOAuthClientId clientId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(mInitialTokenOrNull);
        }

        public Task SaveRefreshTokenAsync(
            GoogleOAuthClientId clientId,
            GoogleRefreshToken refreshToken,
            CancellationToken cancellationToken)
        {
            SavedTokenOrNull = refreshToken;
            return Task.CompletedTask;
        }

        public Task DeleteRefreshTokenAsync(
            GoogleOAuthClientId clientId,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCodeProvider : IGoogleOAuthAuthorizationCodeProvider
    {
        public GoogleOAuthState? StateOrNull { get; private set; }

        public GooglePkceCodeChallenge? CodeChallengeOrNull { get; private set; }

        public Task<GoogleOAuthAuthorizationCodeResult> RequestCodeAsync(
            GoogleOAuthClientId clientId,
            GoogleOAuthState state,
            GooglePkceCodeChallenge codeChallenge,
            CancellationToken cancellationToken)
        {
            StateOrNull = state;
            CodeChallengeOrNull = codeChallenge;
            return Task.FromResult(
                GoogleOAuthAuthorizationCodeResult.Complete(
                    new GoogleOAuthAuthorizationCode("authorization-code"),
                    new GoogleOAuthRedirectUri(
                        new Uri(
                            "http://127.0.0.1:53122/oauth2/callback",
                            UriKind.Absolute))));
        }
    }

    private sealed class PkceTokenHttpMessageHandler : HttpMessageHandler
    {
        private readonly RecordingCodeProvider mCodeProvider;

        public bool PkceVerified { get; private set; }

        public PkceTokenHttpMessageHandler(RecordingCodeProvider codeProvider)
        {
            mCodeProvider = codeProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpContent? contentOrNull = request.Content;
            if (contentOrNull == null)
            {
                throw new InvalidOperationException("The token request does not contain a body.");
            }

            string form = await contentOrNull.ReadAsStringAsync(cancellationToken);
            IReadOnlyDictionary<string, string> parameters = parseForm(form);
            byte[] digest = SHA256.HashData(
                Encoding.ASCII.GetBytes(parameters["code_verifier"]));
            string challenge = Convert.ToBase64String(digest)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            PkceVerified = string.Equals(
                challenge,
                mCodeProvider.CodeChallengeOrNull?.Value,
                StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"access-secret\",\"refresh_token\":\"saved-refresh-token\",\"token_type\":\"Bearer\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
        }

        private static IReadOnlyDictionary<string, string> parseForm(string form)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            foreach (string pair in form.Split('&'))
            {
                string[] parts = pair.Split('=', 2);
                values[Uri.UnescapeDataString(parts[0])] =
                    Uri.UnescapeDataString(parts[1].Replace('+', ' '));
            }

            return values;
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("HTTP must not be called.");
        }
    }

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(
                new TaskCanceledException("Simulated HTTP timeout."));
        }
    }

    private sealed class ServiceUnavailableHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent(
                        "{\"error\":\"temporarily_unavailable\"}",
                        Encoding.UTF8,
                        "application/json"),
                });
        }
    }

    private sealed class ThrowingCredentialStore : IGoogleCalendarCredentialStore
    {
        public Task<GoogleRefreshToken?> ReadRefreshTokenOrNullAsync(
            GoogleOAuthClientId clientId,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Credential storage must not be called.");
        }

        public Task SaveRefreshTokenAsync(
            GoogleOAuthClientId clientId,
            GoogleRefreshToken refreshToken,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Credential storage must not be called.");
        }

        public Task DeleteRefreshTokenAsync(
            GoogleOAuthClientId clientId,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Credential storage must not be called.");
        }
    }

    private sealed class FailingCredentialStore : IGoogleCalendarCredentialStore
    {
        public Task<GoogleRefreshToken?> ReadRefreshTokenOrNullAsync(
            GoogleOAuthClientId clientId,
            CancellationToken cancellationToken)
        {
            throw new Win32Exception(5);
        }

        public Task SaveRefreshTokenAsync(
            GoogleOAuthClientId clientId,
            GoogleRefreshToken refreshToken,
            CancellationToken cancellationToken)
        {
            throw new Win32Exception(5);
        }

        public Task DeleteRefreshTokenAsync(
            GoogleOAuthClientId clientId,
            CancellationToken cancellationToken)
        {
            throw new Win32Exception(5);
        }
    }

    private sealed class ThrowingCodeProvider : IGoogleOAuthAuthorizationCodeProvider
    {
        public Task<GoogleOAuthAuthorizationCodeResult> RequestCodeAsync(
            GoogleOAuthClientId clientId,
            GoogleOAuthState state,
            GooglePkceCodeChallenge codeChallenge,
            CancellationToken cancellationToken)
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
        public void Launch(Uri uri)
        {
        }
    }

    private sealed class ProbeThenCallbackBrowserLauncher : IExternalBrowserLauncher
    {
        private readonly CancellationToken mCancellationToken;

        public Task CallbackTask { get; private set; } = Task.CompletedTask;

        public HttpStatusCode? ProbeStatusCodeOrNull { get; private set; }

        public HttpStatusCode? CallbackStatusCodeOrNull { get; private set; }

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
            string redirectUriValue = getQueryParameter(
                authorizationUri,
                "redirect_uri");
            Uri redirectUri = new Uri(redirectUriValue, UriKind.Absolute);
            Uri authorityUri = new Uri(
                redirectUri.GetLeftPart(UriPartial.Authority),
                UriKind.Absolute);
            using (HttpClientHandler handler = new HttpClientHandler
            {
                UseProxy = false,
            })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage probeResponse = await client.GetAsync(
                new Uri(authorityUri, "/probe"),
                mCancellationToken))
            {
                ProbeStatusCodeOrNull = probeResponse.StatusCode;
            }

            string callbackQuery = "?code=authorization-code&state=opaque-state";
            using (HttpClientHandler handler = new HttpClientHandler
            {
                UseProxy = false,
            })
            using (HttpClient client = new HttpClient(handler))
            using (HttpResponseMessage callbackResponse = await client.GetAsync(
                new Uri(redirectUri.AbsoluteUri + callbackQuery, UriKind.Absolute),
                mCancellationToken))
            {
                CallbackStatusCodeOrNull = callbackResponse.StatusCode;
            }
        }

        private static string getQueryParameter(Uri uri, string parameterName)
        {
            string query = uri.Query.StartsWith("?", StringComparison.Ordinal)
                ? uri.Query[1..]
                : uri.Query;
            foreach (string pair in query.Split('&'))
            {
                string[] parts = pair.Split('=', 2);
                if (parts.Length == 2
                    && string.Equals(
                        Uri.UnescapeDataString(parts[0]),
                        parameterName,
                        StringComparison.Ordinal))
                {
                    return Uri.UnescapeDataString(parts[1]);
                }
            }

            throw new InvalidOperationException(
                "The authorization URL does not contain the expected query parameter.");
        }
    }
}
