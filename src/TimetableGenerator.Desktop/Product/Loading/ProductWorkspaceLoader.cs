using System;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Planning;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Product.Loading;

internal sealed class ProductWorkspaceLoader : IProductWorkspaceDataLoader
{
    private readonly CatalogCacheFileStore mCatalogCacheStore;

    private readonly IPlanningWorkspaceStore mWorkspaceStore;

    private readonly IProductCatalogDownloader mCatalogDownloader;

    internal ProductWorkspaceLoader(
        CatalogCacheFileStore catalogCacheStore,
        IPlanningWorkspaceStore workspaceStore,
        IProductCatalogDownloader catalogDownloader)
    {
        if (catalogCacheStore == null)
        {
            throw new ArgumentNullException(nameof(catalogCacheStore));
        }

        if (workspaceStore == null)
        {
            throw new ArgumentNullException(nameof(workspaceStore));
        }

        if (catalogDownloader == null)
        {
            throw new ArgumentNullException(nameof(catalogDownloader));
        }

        mCatalogCacheStore = catalogCacheStore;
        mWorkspaceStore = workspaceStore;
        mCatalogDownloader = catalogDownloader;
    }

    public async Task<ProductWorkspaceLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        PlanningWorkspaceLoadResult workspaceLoadResult =
            await mWorkspaceStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        CatalogCacheLoadResult cacheLoadResult =
            await mCatalogCacheStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        EProductWorkspaceRecoveryFlags recoveryFlags =
            getWorkspaceRecoveryFlags(workspaceLoadResult);

        if (workspaceLoadResult.IsFound == false)
        {
            return await createWorkspaceAsync(
                cacheLoadResult,
                recoveryFlags,
                cancellationToken).ConfigureAwait(false);
        }

        PlanningWorkspace workspace = getLoadedWorkspace(workspaceLoadResult);
        PlanCatalogBinding savedBinding = getSharedCatalogBinding(workspace);
        if (cacheLoadResult.IsFound)
        {
            return await loadWithCachedCatalogAsync(
                cacheLoadResult,
                workspace,
                savedBinding,
                recoveryFlags,
                cancellationToken).ConfigureAwait(false);
        }

        return await loadWithDownloadedCatalogAsync(
            workspace,
            savedBinding,
            recoveryFlags,
            cancellationToken).ConfigureAwait(false);
    }

    private static PlanningWorkspace createEmptyWorkspace(
        VerifiedCatalogPackage catalogPackage)
    {
        PlanId planId = PlanId.CreateNew();
        PlanCatalogBinding catalogBinding = createCatalogBinding(catalogPackage);
        PlanName initialPlanName =
            AcademicTermPlanNameFactory.CreateInitialPlanName(
                catalogBinding.Term);
        PlanningPlan plan = new PlanningPlan(
            planId,
            initialPlanName,
            catalogBinding,
            new PlanningPlanContent(
                Array.Empty<CourseChoiceGroup>(),
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        return new PlanningWorkspace(planId, new PlanningPlan[] { plan });
    }

    private static PlanCatalogBinding createCatalogBinding(
        VerifiedCatalogPackage catalogPackage)
    {
        return catalogPackage.CreatePlanCatalogBinding();
    }

    private static PlanningWorkspace getLoadedWorkspace(
        PlanningWorkspaceLoadResult loadResult)
    {
        if (loadResult.WorkspaceOrNull == null)
        {
            throw new InvalidOperationException(
                "A found workspace load result must contain a workspace.");
        }

        return loadResult.WorkspaceOrNull;
    }

    private static PlanCatalogBinding getSharedCatalogBinding(
        PlanningWorkspace workspace)
    {
        PlanCatalogBinding sharedBinding = workspace.Plans[0].CatalogBinding;
        foreach (PlanningPlan plan in workspace.Plans)
        {
            if (plan.CatalogBinding != sharedBinding)
            {
                throw new ProductWorkspaceCatalogCompatibilityException(
                    EPlanningWorkspaceCatalogRebindStatus.MixedCatalogBindings);
            }
        }

        return sharedBinding;
    }

    private static bool hasMatchingCatalogBinding(
        VerifiedCatalogPackage catalogPackage,
        PlanCatalogBinding catalogBinding)
    {
        return catalogPackage.CreatePlanCatalogBinding() == catalogBinding;
    }

    private static void requireCompatibleWorkspace(
        VerifiedCatalogPackage catalogPackage,
        PlanningWorkspace workspace)
    {
        PlanningWorkspaceCatalogRebindResult rebindResult =
            PlanningWorkspaceCatalogRebinder.TryRebind(
                catalogPackage.Document.Catalog,
                catalogPackage.CreatePlanCatalogBinding(),
                workspace);
        if (rebindResult.IsRebound == false)
        {
            throw new ProductWorkspaceCatalogCompatibilityException(rebindResult.Status);
        }
    }

    private static PlanningWorkspace rebindWorkspace(
        VerifiedCatalogPackage catalogPackage,
        PlanningWorkspace workspace)
    {
        PlanningWorkspaceCatalogRebindResult rebindResult =
            PlanningWorkspaceCatalogRebinder.TryRebind(
                catalogPackage.Document.Catalog,
                catalogPackage.CreatePlanCatalogBinding(),
                workspace);
        if (rebindResult.IsRebound == false
            || rebindResult.ReboundWorkspaceOrNull == null)
        {
            throw new ProductWorkspaceCatalogCompatibilityException(rebindResult.Status);
        }

        return rebindResult.ReboundWorkspaceOrNull;
    }

    private static EPlanningWorkspaceCatalogRebindStatus
        getTransitionFailureStatus(
            EPlanningCatalogTransitionStatus transitionStatus)
    {
        switch (transitionStatus)
        {
            case EPlanningCatalogTransitionStatus.InstitutionMismatch:
                return EPlanningWorkspaceCatalogRebindStatus.InstitutionMismatch;
            case EPlanningCatalogTransitionStatus.AcademicTermMismatch:
                return EPlanningWorkspaceCatalogRebindStatus.AcademicTermMismatch;
            case EPlanningCatalogTransitionStatus.RevisionNotNewer:
                return EPlanningWorkspaceCatalogRebindStatus.CatalogRevisionNotNewer;
            case EPlanningCatalogTransitionStatus.ArtifactSha256Mismatch:
                return EPlanningWorkspaceCatalogRebindStatus
                    .CatalogArtifactSha256Mismatch;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(transitionStatus),
                    transitionStatus,
                    "Only an ineligible catalog transition has a failure status.");
        }
    }

    private static EProductWorkspaceRecoveryFlags getCatalogRecoveryFlags(
        CatalogCacheLoadResult cacheLoadResult)
    {
        if (cacheLoadResult.Status
            == ECatalogCacheLoadStatus.RecoveredPreviousGeneration)
        {
            return EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration;
        }

        return EProductWorkspaceRecoveryFlags.None;
    }

    private static EProductWorkspaceRecoveryFlags getWorkspaceRecoveryFlags(
        PlanningWorkspaceLoadResult workspaceLoadResult)
    {
        if (workspaceLoadResult.Status
            == EPlanningWorkspaceLoadStatus.RecoveredPreviousGeneration)
        {
            return EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration;
        }

        return EProductWorkspaceRecoveryFlags.None;
    }

    private async Task<ProductWorkspaceLoadResult> createWorkspaceAsync(
        CatalogCacheLoadResult cacheLoadResult,
        EProductWorkspaceRecoveryFlags recoveryFlags,
        CancellationToken cancellationToken)
    {
        VerifiedCatalogPackage catalogPackage;
        EProductCatalogOrigin catalogOrigin;
        if (cacheLoadResult.IsFound)
        {
            catalogPackage = cacheLoadResult.GetPackage();
            catalogOrigin = EProductCatalogOrigin.OfflineCache;
            recoveryFlags |= getCatalogRecoveryFlags(cacheLoadResult);
        }
        else
        {
            catalogPackage = await mCatalogDownloader.DownloadDefaultCatalogAsync(
                cancellationToken).ConfigureAwait(false);
            await mCatalogCacheStore.SaveAsync(catalogPackage, cancellationToken)
                .ConfigureAwait(false);
            catalogOrigin = EProductCatalogOrigin.RemoteDownload;
        }

        PlanningWorkspace workspace = createEmptyWorkspace(catalogPackage);
        await mWorkspaceStore.SaveAsync(workspace, cancellationToken).ConfigureAwait(false);
        recoveryFlags |= EProductWorkspaceRecoveryFlags.WorkspaceCreated;
        return new ProductWorkspaceLoadResult(
            catalogPackage,
            workspace,
            mWorkspaceStore,
            catalogOrigin,
            recoveryFlags);
    }

    private async Task<ProductWorkspaceLoadResult> loadWithCachedCatalogAsync(
        CatalogCacheLoadResult latestCacheLoadResult,
        PlanningWorkspace workspace,
        PlanCatalogBinding savedBinding,
        EProductWorkspaceRecoveryFlags recoveryFlags,
        CancellationToken cancellationToken)
    {
        VerifiedCatalogPackage latestCatalogPackage =
            latestCacheLoadResult.GetPackage();
        if (hasMatchingCatalogBinding(latestCatalogPackage, savedBinding))
        {
            requireCompatibleWorkspace(latestCatalogPackage, workspace);
            recoveryFlags |= getCatalogRecoveryFlags(latestCacheLoadResult);
            return new ProductWorkspaceLoadResult(
                latestCatalogPackage,
                workspace,
                mWorkspaceStore,
                EProductCatalogOrigin.OfflineCache,
                recoveryFlags);
        }

        EPlanningCatalogTransitionStatus transitionStatus =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                savedBinding,
                latestCatalogPackage.CreatePlanCatalogBinding());
        PlanningWorkspaceCatalogRebindResult? rebindResultOrNull = null;
        if (transitionStatus == EPlanningCatalogTransitionStatus.UpgradeEligible)
        {
            rebindResultOrNull = PlanningWorkspaceCatalogRebinder.TryRebind(
                latestCatalogPackage.Document.Catalog,
                latestCatalogPackage.CreatePlanCatalogBinding(),
                workspace);
            if (rebindResultOrNull.IsRebound
                && rebindResultOrNull.ReboundWorkspaceOrNull != null)
            {
                PlanningWorkspace reboundWorkspace =
                    rebindResultOrNull.ReboundWorkspaceOrNull;
                await mWorkspaceStore.SaveAsync(reboundWorkspace, cancellationToken)
                    .ConfigureAwait(false);
                recoveryFlags |= getCatalogRecoveryFlags(latestCacheLoadResult);
                recoveryFlags |=
                    EProductWorkspaceRecoveryFlags.WorkspaceCatalogRebound;
                return new ProductWorkspaceLoadResult(
                    latestCatalogPackage,
                    reboundWorkspace,
                    mWorkspaceStore,
                    EProductCatalogOrigin.OfflineCache,
                    recoveryFlags);
            }
        }

        CatalogCacheLoadResult matchingCacheLoadResult =
            await mCatalogCacheStore.LoadMatchingAsync(
                savedBinding,
                cancellationToken).ConfigureAwait(false);
        if (matchingCacheLoadResult.IsFound)
        {
            VerifiedCatalogPackage matchingCatalogPackage =
                matchingCacheLoadResult.GetPackage();
            requireCompatibleWorkspace(matchingCatalogPackage, workspace);
            recoveryFlags |= getCatalogRecoveryFlags(matchingCacheLoadResult);
            return new ProductWorkspaceLoadResult(
                matchingCatalogPackage,
                workspace,
                mWorkspaceStore,
                EProductCatalogOrigin.OfflineCache,
                recoveryFlags);
        }

        if (rebindResultOrNull != null)
        {
            throw new ProductWorkspaceCatalogCompatibilityException(
                rebindResultOrNull.Status);
        }

        throw new ProductWorkspaceCatalogCompatibilityException(
            getTransitionFailureStatus(transitionStatus));
    }

    private async Task<ProductWorkspaceLoadResult> loadWithDownloadedCatalogAsync(
        PlanningWorkspace workspace,
        PlanCatalogBinding savedBinding,
        EProductWorkspaceRecoveryFlags recoveryFlags,
        CancellationToken cancellationToken)
    {
        VerifiedCatalogPackage downloadedCatalogPackage =
            await mCatalogDownloader.DownloadDefaultCatalogAsync(cancellationToken)
                .ConfigureAwait(false);
        PlanningWorkspace compatibleWorkspace = workspace;
        bool shouldSaveWorkspace = false;
        EPlanningCatalogTransitionStatus transitionStatus =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                savedBinding,
                downloadedCatalogPackage.CreatePlanCatalogBinding());
        if (transitionStatus == EPlanningCatalogTransitionStatus.ExactMatch)
        {
            requireCompatibleWorkspace(downloadedCatalogPackage, workspace);
        }
        else if (transitionStatus
            == EPlanningCatalogTransitionStatus.UpgradeEligible)
        {
            compatibleWorkspace = rebindWorkspace(downloadedCatalogPackage, workspace);
            shouldSaveWorkspace = true;
        }
        else
        {
            throw new ProductWorkspaceCatalogCompatibilityException(
                getTransitionFailureStatus(transitionStatus));
        }

        await mCatalogCacheStore.SaveAsync(downloadedCatalogPackage, cancellationToken)
            .ConfigureAwait(false);
        if (shouldSaveWorkspace)
        {
            await mWorkspaceStore.SaveAsync(compatibleWorkspace, cancellationToken)
                .ConfigureAwait(false);
            recoveryFlags |= EProductWorkspaceRecoveryFlags.WorkspaceCatalogRebound;
        }

        return new ProductWorkspaceLoadResult(
            downloadedCatalogPackage,
            compatibleWorkspace,
            mWorkspaceStore,
            EProductCatalogOrigin.RemoteDownload,
            recoveryFlags);
    }
}
