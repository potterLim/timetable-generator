using System;

using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Product.CatalogUpdates;

internal sealed class ProductCatalogUpdateResult
{
    public EProductCatalogUpdateStatus Status { get; }

    public CatalogRevision CandidateRevision { get; }

    public ProductCatalogUpdateResult(
        EProductCatalogUpdateStatus status,
        CatalogRevision candidateRevision)
    {
        if (Enum.IsDefined(typeof(EProductCatalogUpdateStatus), status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (candidateRevision.IsValid == false)
        {
            throw new ArgumentException(
                "Catalog update results require a valid candidate revision.",
                nameof(candidateRevision));
        }

        Status = status;
        CandidateRevision = candidateRevision;
    }
}
