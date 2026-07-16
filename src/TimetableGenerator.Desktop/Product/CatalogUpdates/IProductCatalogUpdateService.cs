using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Product.CatalogUpdates;

internal interface IProductCatalogUpdateService
{
    Task<ProductCatalogUpdateResult> CheckAndStageAsync(
        VerifiedCatalogPackage activePackage,
        PlanningWorkspace workspaceSnapshot,
        CancellationToken cancellationToken);
}
