using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Product.CatalogUpdates;

internal sealed class ProductCatalogUpdateService : IProductCatalogUpdateService
{
    private readonly IProductCatalogDownloader mCatalogDownloader;

    private readonly CatalogCacheFileStore mCatalogCacheStore;

    public ProductCatalogUpdateService(
        IProductCatalogDownloader catalogDownloader,
        CatalogCacheFileStore catalogCacheStore)
    {
        if (catalogDownloader == null)
        {
            throw new ArgumentNullException(nameof(catalogDownloader));
        }

        if (catalogCacheStore == null)
        {
            throw new ArgumentNullException(nameof(catalogCacheStore));
        }

        mCatalogDownloader = catalogDownloader;
        mCatalogCacheStore = catalogCacheStore;
    }

    public async Task<ProductCatalogUpdateResult> CheckAndStageAsync(
        VerifiedCatalogPackage activePackage,
        PlanningWorkspace workspaceSnapshot,
        CancellationToken cancellationToken)
    {
        if (activePackage == null)
        {
            throw new ArgumentNullException(nameof(activePackage));
        }

        if (workspaceSnapshot == null)
        {
            throw new ArgumentNullException(nameof(workspaceSnapshot));
        }

        PlanCatalogBinding activeBinding = requireWorkspaceBinding(
            activePackage,
            workspaceSnapshot);
        VerifiedCatalogPackage candidatePackage =
            await mCatalogDownloader.DownloadDefaultCatalogAsync(cancellationToken)
                .ConfigureAwait(false);
        EPlanningCatalogTransitionStatus transitionStatus =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                activeBinding,
                candidatePackage.CreatePlanCatalogBinding());
        if (transitionStatus == EPlanningCatalogTransitionStatus.ExactMatch)
        {
            return new ProductCatalogUpdateResult(
                EProductCatalogUpdateStatus.Current,
                candidatePackage.Entry.Revision);
        }

        if (transitionStatus
            == EPlanningCatalogTransitionStatus.ArtifactSha256Mismatch)
        {
            return new ProductCatalogUpdateResult(
                EProductCatalogUpdateStatus.RevisionArtifactChanged,
                candidatePackage.Entry.Revision);
        }

        if (transitionStatus != EPlanningCatalogTransitionStatus.UpgradeEligible)
        {
            return new ProductCatalogUpdateResult(
                EProductCatalogUpdateStatus.TransitionRejected,
                candidatePackage.Entry.Revision);
        }

        PlanningWorkspaceCatalogRebindResult rebindResult =
            PlanningWorkspaceCatalogRebinder.TryRebind(
                candidatePackage.Document.Catalog,
                candidatePackage.CreatePlanCatalogBinding(),
                workspaceSnapshot);
        if (rebindResult.IsRebound == false)
        {
            return new ProductCatalogUpdateResult(
                EProductCatalogUpdateStatus.WorkspaceIncompatible,
                candidatePackage.Entry.Revision);
        }

        await mCatalogCacheStore.SaveRetainingAsync(
            candidatePackage,
            activeBinding,
            cancellationToken).ConfigureAwait(false);
        return new ProductCatalogUpdateResult(
            EProductCatalogUpdateStatus.Staged,
            candidatePackage.Entry.Revision);
    }

    private static PlanCatalogBinding requireWorkspaceBinding(
        VerifiedCatalogPackage activePackage,
        PlanningWorkspace workspaceSnapshot)
    {
        PlanCatalogBinding activeBinding = workspaceSnapshot.CatalogBinding;

        EPlanningCatalogTransitionStatus activeTransitionStatus =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                activeBinding,
                activePackage.CreatePlanCatalogBinding());
        if (activeTransitionStatus != EPlanningCatalogTransitionStatus.ExactMatch)
        {
            throw new ArgumentException(
                "The active workspace must match its verified catalog package.",
                nameof(activePackage));
        }

        return activeBinding;
    }
}
