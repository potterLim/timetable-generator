using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed record PlanCatalogBinding
{
    public CatalogId CatalogId { get; }

    public InstitutionId InstitutionId { get; }

    public AcademicTerm Term { get; }

    public CatalogRevision Revision { get; }

    public CatalogArtifactSha256 ArtifactSha256 { get; }

    public PlanCatalogBinding(
        CatalogId catalogId,
        InstitutionId institutionId,
        AcademicTerm term,
        CatalogRevision revision,
        CatalogArtifactSha256 artifactSha256)
    {
        if (catalogId == null)
        {
            throw new ArgumentNullException(nameof(catalogId));
        }

        if (institutionId == null)
        {
            throw new ArgumentNullException(nameof(institutionId));
        }

        if (term.IsValid == false)
        {
            throw new ArgumentException(
                "Plan catalog bindings require a valid academic term.",
                nameof(term));
        }

        if (revision.IsValid == false)
        {
            throw new ArgumentException(
                "Plan catalog bindings require a valid catalog revision.",
                nameof(revision));
        }

        if (artifactSha256 == null)
        {
            throw new ArgumentNullException(nameof(artifactSha256));
        }

        CatalogId = catalogId;
        InstitutionId = institutionId;
        Term = term;
        Revision = revision;
        ArtifactSha256 = artifactSha256;
    }
}
