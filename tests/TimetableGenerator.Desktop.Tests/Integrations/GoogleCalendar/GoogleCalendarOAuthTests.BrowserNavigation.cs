using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
{
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
}
