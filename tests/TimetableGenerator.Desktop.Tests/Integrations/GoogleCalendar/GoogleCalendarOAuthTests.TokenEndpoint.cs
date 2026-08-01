using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
{
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
}
