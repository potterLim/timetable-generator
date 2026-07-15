using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal sealed record CatalogIndexEntry
{
    public string CatalogId
    {
        get
        {
            return CatalogFileLayout.GetCatalogId(Term, Revision);
        }
    }

    public AcademicTerm Term { get; }
    public CatalogRevision Revision { get; }
    public string RelativePath
    {
        get
        {
            return CatalogFileLayout.GetCatalogRelativePath(Term, Revision);
        }
    }

    public CatalogFileSize FileSize { get; }
    public Sha256Digest Sha256 { get; }
    public CatalogItemCount CourseCount { get; }
    public CatalogItemCount OfferingCount { get; }

    public CatalogIndexEntry(
        AcademicTerm term,
        CatalogRevision revision,
        CatalogFileSize fileSize,
        Sha256Digest sha256,
        CatalogItemCount courseCount,
        CatalogItemCount offeringCount)
    {
        Term = term;
        Revision = revision;
        FileSize = fileSize;
        Sha256 = sha256;
        CourseCount = courseCount;
        OfferingCount = offeringCount;
    }
}
