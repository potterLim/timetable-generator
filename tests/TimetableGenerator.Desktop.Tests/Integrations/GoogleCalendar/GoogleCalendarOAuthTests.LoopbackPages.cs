using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
{
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
}
