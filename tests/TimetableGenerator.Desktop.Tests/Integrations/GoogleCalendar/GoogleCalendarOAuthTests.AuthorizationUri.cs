using System;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
{
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
}
