using System;
using System.Collections.Generic;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal sealed class CatalogIndexDocument
{
    public string DefaultCatalogId { get; }
    public IReadOnlyList<CatalogIndexEntry> Entries { get; }

    public CatalogIndexDocument(CatalogIndexEntry defaultEntry, IEnumerable<CatalogIndexEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(defaultEntry);
        ArgumentNullException.ThrowIfNull(entries);

        List<CatalogIndexEntry> normalizedEntries = new List<CatalogIndexEntry>(entries);
        if (normalizedEntries.Count == 0)
        {
            throw new ArgumentException("A catalog index must contain at least one entry.", nameof(entries));
        }

        normalizedEntries.Sort(compareEntries);
        ensureUniqueCatalogIds(normalizedEntries);
        bool containsDefaultEntry = normalizedEntries.Exists(
            entry => string.Equals(entry.CatalogId, defaultEntry.CatalogId, StringComparison.Ordinal));
        if (containsDefaultEntry == false)
        {
            throw new ArgumentException("The default catalog must be present in the index.", nameof(defaultEntry));
        }

        DefaultCatalogId = defaultEntry.CatalogId;
        Entries = normalizedEntries.AsReadOnly();
    }

    public static CatalogIndexDocument CreateWithUpsertedEntry(CatalogIndexEntry entry, IEnumerable<CatalogIndexEntry> existingEntries)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(existingEntries);

        List<CatalogIndexEntry> entries = new List<CatalogIndexEntry>();
        foreach (CatalogIndexEntry existingEntry in existingEntries)
        {
            if (string.Equals(existingEntry.CatalogId, entry.CatalogId, StringComparison.Ordinal) == false)
            {
                entries.Add(existingEntry);
            }
        }

        entries.Add(entry);
        return new CatalogIndexDocument(entry, entries);
    }

    private static int compareEntries(CatalogIndexEntry left, CatalogIndexEntry right)
    {
        int academicYearComparison = left.Term.AcademicYear.Value.CompareTo(right.Term.AcademicYear.Value);
        if (academicYearComparison != 0)
        {
            return academicYearComparison;
        }

        int semesterComparison = left.Term.Semester.Value.CompareTo(right.Term.Semester.Value);
        if (semesterComparison != 0)
        {
            return semesterComparison;
        }

        return left.Revision.Value.CompareTo(right.Revision.Value);
    }

    private static void ensureUniqueCatalogIds(IReadOnlyList<CatalogIndexEntry> entries)
    {
        HashSet<string> catalogIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CatalogIndexEntry entry in entries)
        {
            if (catalogIds.Add(entry.CatalogId) == false)
            {
                throw new ArgumentException("The catalog index contains a duplicate catalog ID.", nameof(entries));
            }
        }
    }
}
