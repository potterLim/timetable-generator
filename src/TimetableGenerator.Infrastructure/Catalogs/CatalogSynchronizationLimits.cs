using System;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed class CatalogSynchronizationLimits
{
    private const long MAXIMUM_COMBINED_BYTES = int.MaxValue - 4_096L;

    public CatalogResourceByteLimit Index { get; }

    public CatalogResourceByteLimit Catalog { get; }

    internal long MaximumCacheDocumentBytes
    {
        get
        {
            return Index.Bytes + Catalog.Bytes + CatalogCacheBinaryCodec.HEADER_LENGTH;
        }
    }

    public CatalogSynchronizationLimits(CatalogResourceByteLimit index, CatalogResourceByteLimit catalog)
    {
        if (index.IsValid == false)
        {
            throw new ArgumentException("Catalog synchronization requires a valid index limit.", nameof(index));
        }

        if (catalog.IsValid == false)
        {
            throw new ArgumentException("Catalog synchronization requires a valid catalog limit.", nameof(catalog));
        }

        if (index.Bytes + catalog.Bytes > MAXIMUM_COMBINED_BYTES)
        {
            throw new ArgumentException("The combined catalog synchronization limits are too large.", nameof(catalog));
        }

        Index = index;
        Catalog = catalog;
    }
}
