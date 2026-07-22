using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogIndexEntry
{
    public CatalogId CatalogId { get; }

    public InstitutionMetadata Institution { get; }

    public AcademicTerm Term { get; }

    public CatalogRevision Revision { get; }

    public CatalogFileDescriptor File { get; }

    public CatalogIndexCounts Counts { get; }

    public CatalogIndexEntry(
        CatalogId catalogId,
        InstitutionMetadata institution,
        AcademicTerm term,
        CatalogRevision revision,
        CatalogFileDescriptor file,
        CatalogIndexCounts counts)
    {
        if (catalogId == null)
        {
            throw new ArgumentNullException(nameof(catalogId));
        }

        if (institution == null)
        {
            throw new ArgumentNullException(nameof(institution));
        }

        if (term.IsValid == false)
        {
            throw new ArgumentException("Index entries require a valid term.", nameof(term));
        }

        if (revision.IsValid == false)
        {
            throw new ArgumentException("Index entries require a valid revision.", nameof(revision));
        }

        if (file == null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        if (counts == null)
        {
            throw new ArgumentNullException(nameof(counts));
        }

        CatalogId = catalogId;
        Institution = institution;
        Term = term;
        Revision = revision;
        File = file;
        Counts = counts;
    }
}
