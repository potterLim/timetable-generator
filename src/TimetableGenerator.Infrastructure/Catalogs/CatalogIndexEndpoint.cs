using System;
using TimetableGenerator.CatalogJson;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed record CatalogIndexEndpoint
{
    public Uri Value { get; }

    public CatalogIndexEndpoint(Uri value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        bool isHttpsEndpoint = false;
        if (value.IsAbsoluteUri)
        {
            isHttpsEndpoint = string.Equals(value.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }

        if (value.IsAbsoluteUri == false
            || isHttpsEndpoint == false
            || string.IsNullOrWhiteSpace(value.Host)
            || value.UserInfo.Length > 0
            || value.Fragment.Length > 0)
        {
            throw new ArgumentException("Catalog index endpoints must be absolute HTTPS URLs without credentials or fragments.", nameof(value));
        }

        Value = value;
    }

    public Uri ResolveCatalogUri(CatalogRelativePath relativePath)
    {
        if (relativePath == null)
        {
            throw new ArgumentNullException(nameof(relativePath));
        }

        Uri resolvedUri = new Uri(Value, relativePath.Value);
        if (IsSameOrigin(resolvedUri) == false)
        {
            throw new InvalidOperationException("Catalog paths must resolve to the configured index origin.");
        }

        return resolvedUri;
    }

    public bool IsSameOrigin(Uri resourceUri)
    {
        if (resourceUri == null)
        {
            throw new ArgumentNullException(nameof(resourceUri));
        }

        return resourceUri.IsAbsoluteUri
            && string.Equals(Value.Scheme, resourceUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Value.IdnHost, resourceUri.IdnHost, StringComparison.OrdinalIgnoreCase)
            && Value.Port == resourceUri.Port;
    }

    public override string ToString()
    {
        return Value.AbsoluteUri;
    }
}
