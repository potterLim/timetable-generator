using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed record PlanCatalogBinding
{
    public CatalogId CatalogId { get; }

    public AcademicTerm Term { get; }

    public CatalogRevision Revision { get; }

    public PlanCatalogBinding(
        CatalogId catalogId,
        AcademicTerm term,
        CatalogRevision revision)
    {
        if (catalogId == null)
        {
            throw new ArgumentNullException(nameof(catalogId));
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

        CatalogId = catalogId;
        Term = term;
        Revision = revision;
    }
}
