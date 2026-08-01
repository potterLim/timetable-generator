using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
{
    private sealed class FixedConfigurationProvider : IGoogleCalendarOAuthConfigurationProvider
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
                Content = new StringContent("{\"access_token\":\"access-secret\",\"token_type\":\"Bearer\"}", Encoding.UTF8, "application/json"),
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
}
