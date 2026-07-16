using System;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Configuration;

internal sealed class CatalogSourceConfiguration
{
    public CatalogIndexEndpoint Endpoint { get; }

    public ECatalogSourceOrigin Origin { get; }

    public CatalogSourceConfiguration(
        CatalogIndexEndpoint endpoint,
        ECatalogSourceOrigin origin)
    {
        if (endpoint == null)
        {
            throw new ArgumentNullException(nameof(endpoint));
        }

        if (Enum.IsDefined(typeof(ECatalogSourceOrigin), origin) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(origin));
        }

        Endpoint = endpoint;
        Origin = origin;
    }
}
