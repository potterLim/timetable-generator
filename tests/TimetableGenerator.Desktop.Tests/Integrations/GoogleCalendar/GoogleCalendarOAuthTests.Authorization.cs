using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
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
}
