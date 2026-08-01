using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
{
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
                Content = new StringContent("{\"access_token\":\"access-secret\",\"token_type\":\"Bearer\"}", Encoding.UTF8, "application/json"),
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
                Content = new StringContent("{\"error\":\"temporarily_unavailable\"}", Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class OversizedTokenResponseHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(new string('x', 70_000), Encoding.UTF8, "application/json"),
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
                Content = new StringContent(mResponseBody, Encoding.UTF8, "application/json"),
            });
        }
    }
}
