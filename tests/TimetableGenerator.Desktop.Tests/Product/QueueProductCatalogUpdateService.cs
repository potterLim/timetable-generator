using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Product.CatalogUpdates;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Tests.Product;

internal sealed class QueueProductCatalogUpdateService :
    IProductCatalogUpdateService
{
    private readonly Queue<Func<
        VerifiedCatalogPackage,
        PlanningWorkspace,
        CancellationToken,
        Task<ProductCatalogUpdateResult>>> mChecks;

    public int CheckCount { get; private set; }

    public QueueProductCatalogUpdateService(
        IEnumerable<Func<
            VerifiedCatalogPackage,
            PlanningWorkspace,
            CancellationToken,
            Task<ProductCatalogUpdateResult>>> checks)
    {
        if (checks == null)
        {
            throw new ArgumentNullException(nameof(checks));
        }

        mChecks = new Queue<Func<VerifiedCatalogPackage, PlanningWorkspace, CancellationToken, Task<ProductCatalogUpdateResult>>>(checks);
    }

    public Task<ProductCatalogUpdateResult> CheckAndStageAsync(
        VerifiedCatalogPackage activePackage,
        PlanningWorkspace workspaceSnapshot,
        CancellationToken cancellationToken)
    {
        ++CheckCount;
        if (mChecks.Count == 0)
        {
            throw new InvalidOperationException("No queued catalog update check remains.");
        }

        Func<
            VerifiedCatalogPackage,
            PlanningWorkspace,
            CancellationToken,
            Task<ProductCatalogUpdateResult>> check = mChecks.Dequeue();
        return check(activePackage, workspaceSnapshot, cancellationToken);
    }
}
