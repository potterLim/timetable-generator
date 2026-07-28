using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleOAuthRedirectUri
{
    private const string CALLBACK_PATH = "/";

    public Uri Value { get; }

    public GoogleOAuthRedirectUri(Uri value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.IsAbsoluteUri == false
            || string.Equals(value.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) == false
            || string.Equals(value.Host, "127.0.0.1", StringComparison.Ordinal) == false
            || value.IsDefaultPort
            || string.Equals(value.AbsolutePath, CALLBACK_PATH, StringComparison.Ordinal) == false
            || value.Query.Length > 0
            || value.Fragment.Length > 0)
        {
            throw new ArgumentException("Google OAuth redirect URIs must use the product loopback callback.", nameof(value));
        }

        Value = value;
    }
}
