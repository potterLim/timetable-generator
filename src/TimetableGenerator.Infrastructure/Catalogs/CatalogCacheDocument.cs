using System;

namespace TimetableGenerator.Infrastructure.Catalogs;

internal sealed class CatalogCacheDocument
{
    public CatalogCacheGeneration Generation { get; }

    public VerifiedCatalogPackage Package { get; }

    public CatalogCacheDocument(
        CatalogCacheGeneration generation,
        VerifiedCatalogPackage package)
    {
        if (package == null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        Generation = generation;
        Package = package;
    }
}
