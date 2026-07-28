using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogIndexDocument
{
    private readonly IReadOnlyList<CatalogIndexEntry> mEntries;

    public CatalogId DefaultCatalogId { get; }

    public IReadOnlyList<CatalogIndexEntry> Entries
    {
        get
        {
            return mEntries;
        }
    }

    public CatalogIndexDocument(CatalogId defaultCatalogId, IEnumerable<CatalogIndexEntry> entries)
    {
        if (defaultCatalogId == null)
        {
            throw new ArgumentNullException(nameof(defaultCatalogId));
        }

        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        List<CatalogIndexEntry> copiedEntries = new List<CatalogIndexEntry>();
        HashSet<CatalogId> catalogIds = new HashSet<CatalogId>();
        bool hasDefaultEntry = false;
        foreach (CatalogIndexEntry entry in entries)
        {
            if (entry == null)
            {
                throw new ArgumentException("Catalog indexes cannot contain null entries.", nameof(entries));
            }

            if (catalogIds.Add(entry.CatalogId) == false)
            {
                throw new ArgumentException("Catalog indexes cannot contain duplicate catalog IDs.", nameof(entries));
            }

            if (entry.CatalogId == defaultCatalogId)
            {
                hasDefaultEntry = true;
            }

            copiedEntries.Add(entry);
        }

        if (copiedEntries.Count == 0)
        {
            throw new ArgumentException("Catalog indexes require at least one entry.", nameof(entries));
        }

        if (hasDefaultEntry == false)
        {
            throw new ArgumentException("The default catalog ID must reference an index entry.", nameof(defaultCatalogId));
        }

        DefaultCatalogId = defaultCatalogId;
        mEntries = copiedEntries.AsReadOnly();
    }

    public CatalogIndexEntry FindDefaultEntry()
    {
        foreach (CatalogIndexEntry entry in mEntries)
        {
            if (entry.CatalogId == DefaultCatalogId)
            {
                return entry;
            }
        }

        throw new InvalidOperationException("The validated default catalog entry is missing.");
    }
}
