using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        GoogleCalendarOAuthClient client = new GoogleCalendarOAuthClient(new HttpClient(httpHandler), new FixedConfigurationProvider(null), new ThrowingCodeProvider());

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.NotConfigured, result.Status);
        Assert.Equal("oauth_client_not_configured", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task InteractiveAuthorizationUsesPkceWithoutPersistingCredentialsAsync()
    {
        RecordingCodeProvider codeProvider = new RecordingCodeProvider();
        PkceTokenHttpMessageHandler httpHandler = new PkceTokenHttpMessageHandler(codeProvider);
        GoogleCalendarOAuthClient client = new GoogleCalendarOAuthClient(new HttpClient(httpHandler), new FixedConfigurationProvider(new GoogleCalendarOAuthConfiguration(new GoogleOAuthClientId("client.apps.googleusercontent.com"))), codeProvider);

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Completed, result.Status);
        Assert.Equal("[redacted]", result.AccessTokenOrNull?.ToString());
        Assert.True(httpHandler.PkceVerified);
        Assert.False(httpHandler.ClientSecretIncluded);
        Assert.NotEqual(codeProvider.StateOrNull?.Value, codeProvider.CodeChallengeOrNull?.Value);
    }

    [Fact]
    public async Task EveryAuthorizationStartsANewInteractiveFlowAsync()
    {
        RecordingCodeProvider codeProvider = new RecordingCodeProvider();
        PkceTokenHttpMessageHandler httpHandler = new PkceTokenHttpMessageHandler(codeProvider);
        GoogleCalendarOAuthClient client = new GoogleCalendarOAuthClient(new HttpClient(httpHandler), new FixedConfigurationProvider(new GoogleCalendarOAuthConfiguration(new GoogleOAuthClientId("client.apps.googleusercontent.com"))), codeProvider);

        GoogleOAuthAuthorizationResult firstResult = await client.AuthorizeAsync(CancellationToken.None);
        GoogleOAuthAuthorizationResult secondResult = await client.AuthorizeAsync(CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Completed, firstResult.Status);
        Assert.Equal(EGoogleOAuthAuthorizationStatus.Completed, secondResult.Status);
        Assert.Equal(2, codeProvider.RequestCount);
        Assert.Equal(2, httpHandler.RequestCount);
    }

    [Fact]
    public async Task ConfiguredDesktopClientSecretIsSentOnlyToTokenEndpointAsync()
    {
        RecordingCodeProvider codeProvider = new RecordingCodeProvider();
        ClientSecretTokenHttpMessageHandler httpHandler = new ClientSecretTokenHttpMessageHandler();
        GoogleCalendarOAuthClient client = new GoogleCalendarOAuthClient(new HttpClient(httpHandler), new FixedConfigurationProvider(new GoogleCalendarOAuthConfiguration(new GoogleOAuthClientId("client.apps.googleusercontent.com"), new GoogleOAuthClientSecret("native-client-secret"))), codeProvider);

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Completed, result.Status);
        Assert.Equal("native-client-secret", httpHandler.ClientSecretOrNull);
        Assert.Equal(1, httpHandler.ClientSecretParameterCount);
    }

    [Fact]
    public void AuthorizationUrlUsesDesktopLoopbackAndLeastPrivilegeScope()
    {
        Uri redirectUri = new Uri("http://127.0.0.1:53122/", UriKind.Absolute);

        Uri authorizationUri = LoopbackGoogleOAuthAuthorizationCodeProvider.createAuthorizationUri(new GoogleOAuthClientId("client.apps.googleusercontent.com"), new GoogleOAuthRedirectUri(redirectUri), new GoogleOAuthState("state-value"), new GooglePkceCodeChallenge("challenge-value"));

        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A53122", authorizationUri.Query);
        Assert.Contains("code_challenge_method=S256", authorizationUri.Query);
        Assert.Contains("calendar.app.created", Uri.UnescapeDataString(authorizationUri.Query));
        Assert.Contains("calendar.calendarlist.readonly", Uri.UnescapeDataString(authorizationUri.Query));
        Assert.DoesNotContain("access_type", authorizationUri.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt", authorizationUri.Query, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret", authorizationUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void OAuthStateDoesNotExposeItsValueWhenFormatted()
    {
        GoogleOAuthState state = new GoogleOAuthState("state-secret");

        Assert.Equal("[redacted]", state.ToString());
    }

    [Fact]
    public void OAuthClientSecretDoesNotExposeItsValueWhenFormatted()
    {
        GoogleOAuthClientSecret clientSecret = new GoogleOAuthClientSecret("native-client-secret");

        Assert.Equal("[redacted]", clientSecret.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" secret")]
    [InlineData("secret ")]
    [InlineData("secret\r\nvalue")]
    public void OAuthClientSecretRejectsInvalidValues(string value)
    {
        Assert.Throws<ArgumentException>(
            delegate
            {
                _ = new GoogleOAuthClientSecret(value);
            });
    }

    [Fact]
    public void CallbackRejectsMismatchedState()
    {
        Uri redirectUri = new Uri("http://127.0.0.1:53122/", UriKind.Absolute);

        GoogleOAuthAuthorizationCodeResult result = LoopbackGoogleOAuthAuthorizationCodeProvider.parseRequestLine("GET /?code=secret-code&state=wrong HTTP/1.1", new GoogleOAuthRedirectUri(redirectUri), new GoogleOAuthState("expected"));

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("oauth_state_mismatch", result.DiagnosticCodeOrNull);
        Assert.DoesNotContain("secret-code", result.DiagnosticCodeOrNull, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "POST /?code=authorization-code&state=expected HTTP/1.1",
        "invalid_loopback_request")]
    [InlineData(
        "GET /unexpected?code=authorization-code&state=expected HTTP/1.1",
        "invalid_callback_path")]
    [InlineData(
        "GET /?state=expected HTTP/1.1",
        "authorization_code_missing")]
    [InlineData(
        "GET /?code=first&code=second&state=expected HTTP/1.1",
        "invalid_loopback_request")]
    [InlineData(
        "GET /?code=authorization-code&state=expected&state=second HTTP/1.1",
        "invalid_loopback_request")]
    [InlineData(
        "GET /?code=%ZZ&state=expected HTTP/1.1",
        "invalid_loopback_request")]
    [InlineData(
        "GET /?code=authorization-code&state=expected",
        "invalid_loopback_request")]
    [InlineData(
        "GET /?code=authorization-code&state=expected HTTP/1.1 extra",
        "invalid_loopback_request")]
    [InlineData(
        "GET  /?code=authorization-code&state=expected HTTP/1.1",
        "invalid_loopback_request")]
    [InlineData(
        "GET /?code=authorization-code&state=expected HTTP/2",
        "invalid_loopback_request")]
    [InlineData(
        "GET /?code=authorization-code&state=expected HTTP/1.2",
        "invalid_loopback_request")]
    [InlineData(
        "GET /?code=authorization-code&state=expected http/1.1",
        "invalid_loopback_request")]
    public void CallbackRejectsInvalidOrAmbiguousRequests(string requestLine, string expectedDiagnosticCode)
    {
        GoogleOAuthAuthorizationCodeResult result = LoopbackGoogleOAuthAuthorizationCodeProvider.parseRequestLine(requestLine, new GoogleOAuthRedirectUri(new Uri("http://127.0.0.1:53122/", UriKind.Absolute)), new GoogleOAuthState("expected"));

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal(expectedDiagnosticCode, result.DiagnosticCodeOrNull);
        Assert.Null(result.AuthorizationCodeOrNull);
    }

    [Theory]
    [InlineData("HTTP/1.0")]
    [InlineData("HTTP/1.1")]
    public void CallbackAcceptsSupportedHttpVersions(string httpVersion)
    {
        GoogleOAuthAuthorizationCodeResult result = LoopbackGoogleOAuthAuthorizationCodeProvider.parseRequestLine("GET /?code=authorization-code&state=expected " + httpVersion, new GoogleOAuthRedirectUri(new Uri("http://127.0.0.1:53122/", UriKind.Absolute)), new GoogleOAuthState("expected"));

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Completed, result.Status);
        Assert.Equal("authorization-code", result.AuthorizationCodeOrNull?.Value);
    }

    [Fact]
    public async Task OversizedLoopbackRequestLineIsRejectedAsync()
    {
        byte[] requestBytes = Encoding.ASCII.GetBytes(new string('a', 16_385));
        using (MemoryStream stream = new MemoryStream(requestBytes))
        {
            await Assert.ThrowsAsync<IOException>(
                async delegate
                {
                    await LoopbackGoogleOAuthAuthorizationCodeProvider.readRequestLineAsync(stream, CancellationToken.None);
                });
        }
    }

    [Fact]
    public async Task BrowserLaunchFailureReturnsSanitizedFailureAsync()
    {
        LoopbackGoogleOAuthAuthorizationCodeProvider provider = new LoopbackGoogleOAuthAuthorizationCodeProvider(new FailingBrowserLauncher(), TimeSpan.FromSeconds(1.0));

        GoogleOAuthAuthorizationCodeResult result = await provider.RequestCodeAsync(new GoogleOAuthClientId("client.apps.googleusercontent.com"), new GoogleOAuthState("opaque-state"), new GooglePkceCodeChallenge("opaque-challenge"), CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("browser_launch_failed", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public void DefaultBrowserLauncherAcceptsShellHandoffWithoutNewProcess()
    {
        DefaultExternalBrowserLauncher launcher = new DefaultExternalBrowserLauncher(
            delegate (ProcessStartInfo startInfo)
            {
                return null;
            });

        launcher.Launch(new Uri("https://accounts.google.com", UriKind.Absolute));
    }

    [Fact]
    public async Task ShellHandoffWithoutNewProcessContinuesWaitingForAuthorizationAsync()
    {
        DefaultExternalBrowserLauncher launcher = new DefaultExternalBrowserLauncher(
            delegate (ProcessStartInfo startInfo)
            {
                return null;
            });
        LoopbackGoogleOAuthAuthorizationCodeProvider provider = new LoopbackGoogleOAuthAuthorizationCodeProvider(launcher, TimeSpan.FromMilliseconds(25.0));

        GoogleOAuthAuthorizationCodeResult result = await provider.RequestCodeAsync(new GoogleOAuthClientId("client.apps.googleusercontent.com"), new GoogleOAuthState("opaque-state"), new GooglePkceCodeChallenge("opaque-challenge"), CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("authorization_timeout", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public void GoogleCalendarWebNavigatorOpensCalendarLandingPage()
    {
        RecordingBrowserLauncher browserLauncher = new RecordingBrowserLauncher();
        DefaultGoogleCalendarWebNavigator navigator = new DefaultGoogleCalendarWebNavigator(browserLauncher);

        bool wasOpened = navigator.TryOpen();

        Assert.True(wasOpened);
        Assert.Equal(new Uri("https://calendar.google.com/calendar/r", UriKind.Absolute), browserLauncher.LaunchedUriOrNull);
    }

    [Fact]
    public void GoogleCalendarWebNavigatorHandlesSupportedLaunchFailures()
    {
        Exception[] launchFailures =
        {
            new Win32Exception(2),
            new InvalidOperationException("The browser process was not created."),
            new PlatformNotSupportedException("No browser integration is available."),
        };

        foreach (Exception launchFailure in launchFailures)
        {
            DefaultGoogleCalendarWebNavigator navigator = new DefaultGoogleCalendarWebNavigator(new ThrowingBrowserLauncher(launchFailure));

            Assert.False(navigator.TryOpen());
        }
    }

    [Fact]
    public void InteractiveAuthorizationAllowsTenMinutesForAccountSelectionAndConsent()
    {
        Assert.Equal(TimeSpan.FromMinutes(10.0), LoopbackGoogleOAuthAuthorizationCodeProvider.DEFAULT_AUTHORIZATION_TIMEOUT);
    }

    [Fact]
    public async Task TokenEndpointTimeoutIsReportedAsNetworkFailureAsync()
    {
        GoogleCalendarOAuthClient client = createInteractiveClient(new TimeoutHttpMessageHandler());

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.NetworkFailed, result.Status);
        Assert.Equal("oauth_timeout", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task TokenEndpointServiceFailureIsReportedAsNetworkFailureAsync()
    {
        GoogleCalendarOAuthClient client = createInteractiveClient(new ServiceUnavailableHttpMessageHandler());

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.NetworkFailed, result.Status);
        Assert.Equal("oauth_service_unavailable", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task MissingDesktopClientSecretUsesSanitizedDiagnosticCodeAsync()
    {
        const string SENSITIVE_DESCRIPTION = "client_secret is missing for account someone@example.com";
        GoogleCalendarOAuthClient client = createInteractiveClient(new TokenErrorHttpMessageHandler(HttpStatusCode.BadRequest, "{\"error\":\"invalid_request\",\"error_description\":\"" + SENSITIVE_DESCRIPTION + "\"}"));

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("oauth_client_secret_required", result.DiagnosticCodeOrNull);
        Assert.DoesNotContain("someone@example.com", result.DiagnosticCodeOrNull, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("invalid_request", "oauth_invalid_request")]
    [InlineData("invalid_client", "oauth_invalid_client")]
    [InlineData("invalid_grant", "oauth_invalid_grant")]
    [InlineData("unauthorized_client", "oauth_unauthorized_client")]
    [InlineData("access_denied", "oauth_access_denied")]
    public async Task TokenEndpointErrorsUseStableDiagnosticCodesAsync(string error, string expectedDiagnosticCode)
    {
        GoogleCalendarOAuthClient client = createInteractiveClient(new TokenErrorHttpMessageHandler(HttpStatusCode.BadRequest, "{\"error\":\"" + error + "\"}"));

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal(expectedDiagnosticCode, result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task OversizedTokenResponseIsRejectedAsync()
    {
        GoogleCalendarOAuthClient client = createInteractiveClient(new OversizedTokenResponseHttpMessageHandler());

        GoogleOAuthAuthorizationResult result = await client.AuthorizeAsync(CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("oauth_response_too_large", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task LoopbackWaitHonorsProductTimeoutAsync()
    {
        LoopbackGoogleOAuthAuthorizationCodeProvider provider = new LoopbackGoogleOAuthAuthorizationCodeProvider(new RecordingBrowserLauncher(), TimeSpan.FromMilliseconds(25.0));

        GoogleOAuthAuthorizationCodeResult result = await provider.RequestCodeAsync(new GoogleOAuthClientId("client.apps.googleusercontent.com"), new GoogleOAuthState("opaque-state"), new GooglePkceCodeChallenge("opaque-challenge"), CancellationToken.None);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Failed, result.Status);
        Assert.Equal("authorization_timeout", result.DiagnosticCodeOrNull);
    }

    [Fact]
    public async Task LoopbackIgnoresProbeAndReturnsSanitizedApprovalPageAsync()
    {
        ProbeThenCallbackBrowserLauncher launcher = new ProbeThenCallbackBrowserLauncher(TestContext.Current.CancellationToken);
        LoopbackGoogleOAuthAuthorizationCodeProvider provider = new LoopbackGoogleOAuthAuthorizationCodeProvider(launcher, TimeSpan.FromSeconds(3.0));

        GoogleOAuthAuthorizationCodeResult result = await provider.RequestCodeAsync(new GoogleOAuthClientId("client.apps.googleusercontent.com"), new GoogleOAuthState("opaque-state"), new GooglePkceCodeChallenge("opaque-challenge"), TestContext.Current.CancellationToken);
        await launcher.CallbackTask.WaitAsync(TimeSpan.FromSeconds(2.0), TestContext.Current.CancellationToken);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Completed, result.Status);
        Assert.Equal("authorization-code", result.AuthorizationCodeOrNull?.Value);
        Assert.Equal(HttpStatusCode.BadRequest, launcher.ProbeStatusCodeOrNull);
        Assert.Equal(HttpStatusCode.OK, launcher.CallbackStatusCodeOrNull);
        Assert.Null(launcher.CallbackRedirectUriOrNull);
        string callbackBody = Assert.IsType<string>(launcher.CallbackBodyOrNull);
        string callbackContentSecurityPolicy = Assert.IsType<string>(launcher.CallbackContentSecurityPolicyOrNull);
        Assert.DoesNotContain("authorization-code", callbackBody, StringComparison.Ordinal);
        Assert.DoesNotContain("opaque-state", callbackBody, StringComparison.Ordinal);
        Assert.Contains("no-store", launcher.CallbackCacheControlOrNull);
        Assert.Equal("no-referrer", launcher.CallbackReferrerPolicyOrNull);
        Assert.Contains("<style>", callbackBody);
        Assert.Contains("Google 승인이 완료되었습니다", callbackBody);
        Assert.Contains("Timetable Generator에서 내보내기를 마무리하고 있습니다.", callbackBody);
        Assert.DoesNotContain("내보냈습니다", callbackBody);
        Assert.DoesNotContain("내보내기 완료", callbackBody);
        string callbackScript = extractInlineElement(callbackBody, "script");
        Assert.Contains("history.replaceState", callbackScript);
        Assert.True(callbackScript.Contains("'/'", StringComparison.Ordinal) || callbackScript.Contains("\"/\"", StringComparison.Ordinal), "The callback script must replace the sensitive URL with the loopback root.");
        Assert.Contains("window.close", callbackScript);
        string callbackScriptHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(callbackScript)));
        Assert.Contains("script-src 'sha256-" + callbackScriptHash + "'", callbackContentSecurityPolicy);
        Assert.Contains("lang=\"ko\"", launcher.ProbeBodyOrNull);
        Assert.Contains("올바른 Google 로그인 응답을 기다리고 있습니다.", launcher.ProbeBodyOrNull);
        Assert.Contains("default-src 'none'", launcher.ProbeContentSecurityPolicyOrNull);
        string probeScript = extractInlineElement(Assert.IsType<string>(launcher.ProbeBodyOrNull), "script");
        Assert.Contains("history.replaceState", probeScript);
        Assert.DoesNotContain("window.close", probeScript);
        string probeScriptHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(probeScript)));
        Assert.Contains("script-src 'sha256-" + probeScriptHash + "'", launcher.ProbeContentSecurityPolicyOrNull);
    }

    [Fact]
    public async Task AccessDeniedCallbackClearsSensitiveHistoryWithoutClosingPageAsync()
    {
        AccessDeniedCallbackBrowserLauncher launcher = new AccessDeniedCallbackBrowserLauncher(TestContext.Current.CancellationToken);
        LoopbackGoogleOAuthAuthorizationCodeProvider provider = new LoopbackGoogleOAuthAuthorizationCodeProvider(launcher, TimeSpan.FromSeconds(3.0));

        GoogleOAuthAuthorizationCodeResult result = await provider.RequestCodeAsync(new GoogleOAuthClientId("client.apps.googleusercontent.com"), new GoogleOAuthState("opaque-state"), new GooglePkceCodeChallenge("opaque-challenge"), TestContext.Current.CancellationToken);
        await launcher.CallbackTask.WaitAsync(TimeSpan.FromSeconds(2.0), TestContext.Current.CancellationToken);

        Assert.Equal(EGoogleOAuthAuthorizationStatus.Cancelled, result.Status);
        Assert.Equal("access_denied", result.DiagnosticCodeOrNull);
        Assert.Equal(HttpStatusCode.OK, launcher.CallbackStatusCodeOrNull);
        string callbackBody = Assert.IsType<string>(launcher.CallbackBodyOrNull);
        Assert.DoesNotContain("opaque-state", callbackBody, StringComparison.Ordinal);
        string callbackScript = extractInlineElement(callbackBody, "script");
        Assert.Contains("history.replaceState", callbackScript);
        Assert.DoesNotContain("window.close", callbackScript);
        string callbackScriptHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(callbackScript)));
        Assert.Contains("script-src 'sha256-" + callbackScriptHash + "'", launcher.CallbackContentSecurityPolicyOrNull);
    }

    private static string extractInlineElement(string html, string elementName)
    {
        string openingTag = "<" + elementName + ">";
        string closingTag = "</" + elementName + ">";
        int contentStartIndex = html.IndexOf(openingTag, StringComparison.Ordinal);
        Assert.True(contentStartIndex >= 0, "The callback page does not contain an inline " + elementName + ".");
        contentStartIndex += openingTag.Length;
        int contentEndIndex = html.IndexOf(closingTag, contentStartIndex, StringComparison.Ordinal);
        Assert.True(contentEndIndex >= contentStartIndex, "The callback page contains an incomplete " + elementName + ".");
        return html[contentStartIndex..contentEndIndex];
    }

    private static GoogleCalendarOAuthClient createInteractiveClient(HttpMessageHandler handler)
    {
        return new GoogleCalendarOAuthClient(new HttpClient(handler), new FixedConfigurationProvider(new GoogleCalendarOAuthConfiguration(new GoogleOAuthClientId("client.apps.googleusercontent.com"))), new RecordingCodeProvider());
    }

    private sealed class FixedConfigurationProvider
        : IGoogleCalendarOAuthConfigurationProvider
    {
        private readonly GoogleCalendarOAuthConfiguration? mConfigurationOrNull;

        public FixedConfigurationProvider(GoogleCalendarOAuthConfiguration? configurationOrNull)
        {
            mConfigurationOrNull = configurationOrNull;
        }

        public GoogleCalendarOAuthConfiguration? GetConfigurationOrNull()
        {
            return mConfigurationOrNull;
        }
    }

    private sealed class RecordingCodeProvider : IGoogleOAuthAuthorizationCodeProvider
    {
        public int RequestCount { get; private set; }

        public GoogleOAuthState? StateOrNull { get; private set; }

        public GooglePkceCodeChallenge? CodeChallengeOrNull { get; private set; }

        public Task<GoogleOAuthAuthorizationCodeResult> RequestCodeAsync(GoogleOAuthClientId clientId, GoogleOAuthState state, GooglePkceCodeChallenge codeChallenge, CancellationToken cancellationToken)
        {
            RequestCount++;
            StateOrNull = state;
            CodeChallengeOrNull = codeChallenge;
            return Task.FromResult(GoogleOAuthAuthorizationCodeResult.Complete(new GoogleOAuthAuthorizationCode("authorization-code"), new GoogleOAuthRedirectUri(new Uri("http://127.0.0.1:53122/", UriKind.Absolute))));
        }
    }

    private sealed class PkceTokenHttpMessageHandler : HttpMessageHandler
    {
        private readonly RecordingCodeProvider mCodeProvider;

        public bool PkceVerified { get; private set; }

        public bool ClientSecretIncluded { get; private set; }

        public int RequestCount { get; private set; }

        public PkceTokenHttpMessageHandler(RecordingCodeProvider codeProvider)
        {
            mCodeProvider = codeProvider;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            HttpContent? contentOrNull = request.Content;
            if (contentOrNull == null)
            {
                throw new InvalidOperationException("The token request does not contain a body.");
            }

            string form = await contentOrNull.ReadAsStringAsync(cancellationToken);
            IReadOnlyDictionary<string, string> parameters = parseForm(form);
            ClientSecretIncluded = parameters.ContainsKey("client_secret");
            byte[] digest = SHA256.HashData(Encoding.ASCII.GetBytes(parameters["code_verifier"]));
            string challenge = Convert.ToBase64String(digest).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            PkceVerified = string.Equals(challenge, mCodeProvider.CodeChallengeOrNull?.Value, StringComparison.Ordinal);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"access-secret\",\"token_type\":\"Bearer\"}",
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
                values[Uri.UnescapeDataString(parts[0])] = Uri.UnescapeDataString(parts[1].Replace('+', ' '));
            }

            return values;
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("HTTP must not be called.");
        }
    }

    private sealed class ClientSecretTokenHttpMessageHandler : HttpMessageHandler
    {
        public string? ClientSecretOrNull { get; private set; }

        public int ClientSecretParameterCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpContent? contentOrNull = request.Content;
            if (contentOrNull == null)
            {
                throw new InvalidOperationException("The token request does not contain a body.");
            }

            string form = await contentOrNull.ReadAsStringAsync(cancellationToken);
            foreach (string pair in form.Split('&'))
            {
                string[] parts = pair.Split('=', 2);
                string name = Uri.UnescapeDataString(parts[0]);
                if (string.Equals(name, "client_secret", StringComparison.Ordinal))
                {
                    ClientSecretParameterCount++;
                    ClientSecretOrNull = Uri.UnescapeDataString(parts[1].Replace('+', ' '));
                }
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"access-secret\",\"token_type\":\"Bearer\"}",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    private sealed class TimeoutHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromException<HttpResponseMessage>(new TaskCanceledException("Simulated HTTP timeout."));
        }
    }

    private sealed class ServiceUnavailableHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(
                        "{\"error\":\"temporarily_unavailable\"}",
                        Encoding.UTF8,
                        "application/json"),
            });
        }
    }

    private sealed class OversizedTokenResponseHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                        new string('x', 70_000),
                        Encoding.UTF8,
                        "application/json"),
            });
        }
    }

    private sealed class TokenErrorHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode mStatusCode;

        private readonly string mResponseBody;

        public TokenErrorHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
        {
            mStatusCode = statusCode;
            mResponseBody = responseBody;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(mStatusCode)
            {
                Content = new StringContent(
                        mResponseBody,
                        Encoding.UTF8,
                        "application/json"),
            });
        }
    }

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

    private sealed class AccessDeniedCallbackBrowserLauncher
        : IExternalBrowserLauncher
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
