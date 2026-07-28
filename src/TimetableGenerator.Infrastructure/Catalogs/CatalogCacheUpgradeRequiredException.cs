using System;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed class CatalogCacheUpgradeRequiredException : CatalogCachePersistenceException
{
    public int UnsupportedSchemaVersion { get; }

    public CatalogCacheUpgradeRequiredException(int unsupportedSchemaVersion, Exception innerException)
        : base("The cached catalog was written by a newer application version.", innerException)
    {
        UnsupportedSchemaVersion = unsupportedSchemaVersion;
    }
}
