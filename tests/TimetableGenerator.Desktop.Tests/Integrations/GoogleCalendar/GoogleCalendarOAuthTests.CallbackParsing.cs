using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
{
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
    [InlineData("POST /?code=authorization-code&state=expected HTTP/1.1", "invalid_loopback_request")]
    [InlineData("GET /unexpected?code=authorization-code&state=expected HTTP/1.1", "invalid_callback_path")]
    [InlineData("GET /?state=expected HTTP/1.1", "authorization_code_missing")]
    [InlineData("GET /?code=first&code=second&state=expected HTTP/1.1", "invalid_loopback_request")]
    [InlineData("GET /?code=authorization-code&state=expected&state=second HTTP/1.1", "invalid_loopback_request")]
    [InlineData("GET /?code=%ZZ&state=expected HTTP/1.1", "invalid_loopback_request")]
    [InlineData("GET /?code=authorization-code&state=expected", "invalid_loopback_request")]
    [InlineData("GET /?code=authorization-code&state=expected HTTP/1.1 extra", "invalid_loopback_request")]
    [InlineData("GET  /?code=authorization-code&state=expected HTTP/1.1", "invalid_loopback_request")]
    [InlineData("GET /?code=authorization-code&state=expected HTTP/2", "invalid_loopback_request")]
    [InlineData("GET /?code=authorization-code&state=expected HTTP/1.2", "invalid_loopback_request")]
    [InlineData("GET /?code=authorization-code&state=expected http/1.1", "invalid_loopback_request")]
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
}
