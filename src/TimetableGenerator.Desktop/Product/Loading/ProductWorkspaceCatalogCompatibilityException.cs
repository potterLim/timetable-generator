using System;
using TimetableGenerator.Application.Planning;

namespace TimetableGenerator.Desktop.Product.Loading;

internal sealed class ProductWorkspaceCatalogCompatibilityException : Exception
{
    public EPlanningWorkspaceCatalogRebindStatus RebindStatus { get; }

    public ProductWorkspaceCatalogCompatibilityException(
        EPlanningWorkspaceCatalogRebindStatus rebindStatus)
        : base(
            "The saved planning workspace is incompatible with the selected catalog: "
            + rebindStatus
            + ".")
    {
        if (Enum.IsDefined(
            typeof(EPlanningWorkspaceCatalogRebindStatus),
            rebindStatus) == false
            || rebindStatus == EPlanningWorkspaceCatalogRebindStatus.Rebound)
        {
            throw new ArgumentOutOfRangeException(nameof(rebindStatus));
        }

        RebindStatus = rebindStatus;
    }
}
