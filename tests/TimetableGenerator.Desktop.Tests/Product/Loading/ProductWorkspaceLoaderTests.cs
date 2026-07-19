using System;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Product.Loading;

public sealed class ProductWorkspaceLoaderTests
{
    [Fact]
    public async Task CachedExactWorkspaceLoadsWithoutDownloaderAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage catalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(revision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(revision);
        PlanningWorkspaceLoadResult workspaceLoadResult =
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            workspaceLoadResult))
        {
            await context.CatalogCacheStore.SaveAsync(
                catalogPackage,
                CancellationToken.None);

            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);

            Assert.Equal(EProductCatalogOrigin.OfflineCache, result.CatalogOrigin);
            Assert.Equal(EProductWorkspaceRecoveryFlags.None, result.RecoveryFlags);
            Assert.Equal(
                "저장된 시간표",
                result.Workspace.GetActivePlan().Name.Value);
            Assert.Equal(
                catalogPackage.Document.Catalog.Id,
                result.CatalogPackage.Document.Catalog.Id);
            Assert.Same(workspace, result.Workspace);
            Assert.Same(context.WorkspaceStore, result.WorkspaceStore);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task CachedChangedArtifactAtSavedRevisionIsRejectedAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage changedArtifactPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackageWithoutSavedCourse(
                revision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(revision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                changedArtifactPackage,
                CancellationToken.None);

            ProductWorkspaceCatalogCompatibilityException exception =
                await Assert.ThrowsAsync<ProductWorkspaceCatalogCompatibilityException>(
                    async delegate
                    {
                        await context.Loader.LoadAsync(CancellationToken.None);
                    });

            Assert.Equal(
                EPlanningWorkspaceCatalogRebindStatus.CatalogArtifactSha256Mismatch,
                exception.RebindStatus);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task DownloadedChangedArtifactAtSavedRevisionIsNotInstalledAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage changedArtifactPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackageWithoutSavedCourse(
                revision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(revision);
        Func<CancellationToken, Task<VerifiedCatalogPackage>> download =
            delegate (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(changedArtifactPackage);
            };
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace),
            download))
        {
            ProductWorkspaceCatalogCompatibilityException exception =
                await Assert.ThrowsAsync<ProductWorkspaceCatalogCompatibilityException>(
                    async delegate
                    {
                        await context.Loader.LoadAsync(CancellationToken.None);
                    });
            CatalogCacheLoadResult cacheLoadResult =
                await context.CatalogCacheStore.LoadAsync(CancellationToken.None);

            Assert.Equal(
                EPlanningWorkspaceCatalogRebindStatus.CatalogArtifactSha256Mismatch,
                exception.RebindStatus);
            Assert.False(cacheLoadResult.IsFound);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
            Assert.Equal(1, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public void LoadResultRejectsChangedArtifactAtWorkspaceRevision()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage changedArtifactPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackageWithoutSavedCourse(
                revision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateEmptyWorkspace(revision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            Assert.Throws<ArgumentException>(
                () => new ProductWorkspaceLoadResult(
                    changedArtifactPackage,
                    workspace,
                    context.WorkspaceStore,
                    EProductCatalogOrigin.OfflineCache,
                    EProductWorkspaceRecoveryFlags.None));
        }
    }

    [Fact]
    public async Task MissingWorkspaceCreatesAndPersistsDefaultPlanFromCacheAsync()
    {
        VerifiedCatalogPackage catalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(new CatalogRevision(1));
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateNotFound()))
        {
            await context.CatalogCacheStore.SaveAsync(
                catalogPackage,
                CancellationToken.None);

            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);

            PlanningPlan activePlan = result.Workspace.GetActivePlan();
            Assert.Equal(EProductCatalogOrigin.OfflineCache, result.CatalogOrigin);
            Assert.True(result.WasWorkspaceCreated);
            Assert.Equal("2026-2학기 시간표", activePlan.Name.Value);
            Assert.Empty(activePlan.CourseChoiceGroups);
            Assert.Empty(activePlan.UnscheduledOfferingSelections);
            Assert.Equal(catalogPackage.Entry.CatalogId, activePlan.CatalogBinding.CatalogId);
            Assert.Equal(
                catalogPackage.Entry.Institution.Id,
                activePlan.CatalogBinding.InstitutionId);
            Assert.Equal(catalogPackage.Entry.Term, activePlan.CatalogBinding.Term);
            Assert.Equal(catalogPackage.Entry.Revision, activePlan.CatalogBinding.Revision);
            Assert.Equal(
                catalogPackage.CreatePlanCatalogBinding().ArtifactSha256,
                activePlan.CatalogBinding.ArtifactSha256);
            Assert.Same(
                result.Workspace,
                Assert.Single(context.WorkspaceStore.SavedWorkspaces));
            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task IncompatibleLatestCatalogFallsBackToMatchingPreviousCatalogAsync()
    {
        CatalogRevision savedRevision = new CatalogRevision(1);
        CatalogRevision latestRevision = new CatalogRevision(2);
        VerifiedCatalogPackage savedCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(savedRevision);
        VerifiedCatalogPackage latestCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackageWithoutSavedCourse(
                latestRevision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(
                savedRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                savedCatalogPackage,
                CancellationToken.None);
            await context.CatalogCacheStore.SaveAsync(
                latestCatalogPackage,
                CancellationToken.None);

            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);

            Assert.Equal(savedRevision, result.CatalogPackage.Entry.Revision);
            Assert.Same(workspace, result.Workspace);
            Assert.True(result.WasCatalogCacheRecovered);
            Assert.False(result.WasWorkspaceCatalogRebound);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task EligibleLatestCatalogIsPreferredOverMatchingPreviousCatalogAsync()
    {
        CatalogRevision savedRevision = new CatalogRevision(1);
        CatalogRevision latestRevision = new CatalogRevision(2);
        VerifiedCatalogPackage savedCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(savedRevision);
        VerifiedCatalogPackage latestCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(latestRevision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(
                savedRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                savedCatalogPackage,
                CancellationToken.None);
            await context.CatalogCacheStore.SaveAsync(
                latestCatalogPackage,
                CancellationToken.None);

            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);

            Assert.Equal(latestRevision, result.CatalogPackage.Entry.Revision);
            Assert.Equal(
                latestRevision,
                result.Workspace.GetActivePlan().CatalogBinding.Revision);
            Assert.Equal(
                latestCatalogPackage.CreatePlanCatalogBinding().ArtifactSha256,
                result.Workspace.GetActivePlan().CatalogBinding.ArtifactSha256);
            Assert.True(result.WasWorkspaceCatalogRebound);
            Assert.Same(
                result.Workspace,
                Assert.Single(context.WorkspaceStore.SavedWorkspaces));
            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task CachedRollbackFallsBackToMatchingCurrentCatalogAsync()
    {
        CatalogRevision currentRevision = new CatalogRevision(2);
        CatalogRevision rollbackRevision = new CatalogRevision(1);
        VerifiedCatalogPackage currentCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(currentRevision);
        VerifiedCatalogPackage rollbackCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(rollbackRevision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(
                currentRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                currentCatalogPackage,
                CancellationToken.None);
            await context.CatalogCacheStore.SaveAsync(
                rollbackCatalogPackage,
                CancellationToken.None);

            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);

            Assert.Equal(currentRevision, result.CatalogPackage.Entry.Revision);
            Assert.Same(workspace, result.Workspace);
            Assert.False(result.WasWorkspaceCatalogRebound);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
        }
    }

    [Fact]
    public async Task DifferentInstitutionCatalogFallsBackToMatchingCurrentCatalogAsync()
    {
        CatalogRevision currentRevision = new CatalogRevision(1);
        VerifiedCatalogPackage currentCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(currentRevision);
        VerifiedCatalogPackage otherInstitutionCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(
                new CatalogRevision(2),
                new InstitutionId("another-university"),
                AcademicTerm.Parse("2026-2"));
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(
                currentRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                currentCatalogPackage,
                CancellationToken.None);
            await context.CatalogCacheStore.SaveAsync(
                otherInstitutionCatalogPackage,
                CancellationToken.None);

            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);

            Assert.Equal(
                currentCatalogPackage.Entry.CatalogId,
                result.CatalogPackage.Entry.CatalogId);
            Assert.Same(workspace, result.Workspace);
            Assert.False(result.WasWorkspaceCatalogRebound);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
        }
    }

    [Fact]
    public async Task DifferentTermCatalogFallsBackToMatchingCurrentCatalogAsync()
    {
        CatalogRevision currentRevision = new CatalogRevision(1);
        VerifiedCatalogPackage currentCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(currentRevision);
        VerifiedCatalogPackage otherTermCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(
                new CatalogRevision(2),
                new InstitutionId("handong-global-university"),
                AcademicTerm.Parse("2027-1"));
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(
                currentRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                currentCatalogPackage,
                CancellationToken.None);
            await context.CatalogCacheStore.SaveAsync(
                otherTermCatalogPackage,
                CancellationToken.None);

            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);

            Assert.Equal(
                currentCatalogPackage.Entry.CatalogId,
                result.CatalogPackage.Entry.CatalogId);
            Assert.Same(workspace, result.Workspace);
            Assert.False(result.WasWorkspaceCatalogRebound);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
        }
    }

    [Fact]
    public async Task LatestCatalogSafelyRebindsAndPersistsWorkspaceAsync()
    {
        CatalogRevision savedRevision = new CatalogRevision(1);
        CatalogRevision latestRevision = new CatalogRevision(2);
        VerifiedCatalogPackage latestCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(latestRevision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(
                savedRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                latestCatalogPackage,
                CancellationToken.None);

            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);

            PlanningPlan reboundPlan = result.Workspace.GetActivePlan();
            Assert.Equal(latestRevision, reboundPlan.CatalogBinding.Revision);
            Assert.True(result.WasWorkspaceCatalogRebound);
            Assert.False(result.WasCatalogCacheRecovered);
            Assert.Single(reboundPlan.CourseChoiceGroups);
            Assert.Same(
                result.Workspace,
                Assert.Single(context.WorkspaceStore.SavedWorkspaces));
            Assert.Equal(savedRevision, workspace.GetActivePlan().CatalogBinding.Revision);
            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task IncompatibleWorkspaceThrowsWithoutResetOrSaveAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage catalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(revision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithMissingOffering(revision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                catalogPackage,
                CancellationToken.None);

            ProductWorkspaceCatalogCompatibilityException exception =
                await Assert.ThrowsAsync<ProductWorkspaceCatalogCompatibilityException>(
                    async delegate
                    {
                        await context.Loader.LoadAsync(CancellationToken.None);
                    });

            Assert.Equal(
                EPlanningWorkspaceCatalogRebindStatus.OfferingNotFound,
                exception.RebindStatus);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
            Assert.Equal(
                "missing-offering",
                workspace.GetActivePlan()
                    .CourseChoiceGroups[0]
                    .CourseCandidates[0]
                    .OfferingCandidates[0]
                    .OfferingId
                    .Value);
        }
    }

    [Fact]
    public async Task MixedWorkspaceBindingsBlockCatalogSelectionAsync()
    {
        VerifiedCatalogPackage catalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(new CatalogRevision(2));
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateMixedBindingWorkspace();
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                catalogPackage,
                CancellationToken.None);

            ProductWorkspaceCatalogCompatibilityException exception =
                await Assert.ThrowsAsync<ProductWorkspaceCatalogCompatibilityException>(
                    async delegate
                    {
                        await context.Loader.LoadAsync(CancellationToken.None);
                    });

            Assert.Equal(
                EPlanningWorkspaceCatalogRebindStatus.MixedCatalogBindings,
                exception.RebindStatus);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task FirstRunDownloadsCachesAndPersistsWorkspaceAsync()
    {
        VerifiedCatalogPackage catalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(new CatalogRevision(1));
        Func<CancellationToken, Task<VerifiedCatalogPackage>> download =
            delegate (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(catalogPackage);
            };
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateNotFound(),
            download))
        {
            ProductWorkspaceLoadResult firstResult = await context.Loader.LoadAsync(
                CancellationToken.None);
            CatalogCacheLoadResult installedCache =
                await context.CatalogCacheStore.LoadAsync(CancellationToken.None);

            Assert.Equal(EProductCatalogOrigin.RemoteDownload, firstResult.CatalogOrigin);
            Assert.True(firstResult.WasWorkspaceCreated);
            Assert.True(installedCache.IsFound);
            Assert.Equal(catalogPackage.Entry.CatalogId, installedCache.GetPackage().Entry.CatalogId);
            Assert.Same(
                firstResult.Workspace,
                Assert.Single(context.WorkspaceStore.SavedWorkspaces));

            ProductWorkspaceLoadResult secondResult = await context.Loader.LoadAsync(
                CancellationToken.None);

            Assert.Equal(EProductCatalogOrigin.OfflineCache, secondResult.CatalogOrigin);
            Assert.Equal(1, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task FirstRunSafelyRebindsSavedWorkspaceBeforeInstallingDownloadAsync()
    {
        CatalogRevision savedRevision = new CatalogRevision(1);
        CatalogRevision downloadedRevision = new CatalogRevision(2);
        VerifiedCatalogPackage downloadedCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(downloadedRevision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(
                savedRevision);
        Func<CancellationToken, Task<VerifiedCatalogPackage>> download =
            delegate (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(downloadedCatalogPackage);
            };
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace),
            download))
        {
            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);
            CatalogCacheLoadResult installedCache =
                await context.CatalogCacheStore.LoadAsync(CancellationToken.None);

            Assert.Equal(EProductCatalogOrigin.RemoteDownload, result.CatalogOrigin);
            Assert.True(result.WasWorkspaceCatalogRebound);
            Assert.Equal(
                downloadedRevision,
                result.Workspace.GetActivePlan().CatalogBinding.Revision);
            Assert.Equal(
                downloadedCatalogPackage.CreatePlanCatalogBinding().ArtifactSha256,
                result.Workspace.GetActivePlan().CatalogBinding.ArtifactSha256);
            Assert.Equal(
                downloadedRevision,
                installedCache.GetPackage().Entry.Revision);
            Assert.Same(
                result.Workspace,
                Assert.Single(context.WorkspaceStore.SavedWorkspaces));
            Assert.Equal(1, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task IncompatibleDownloadedCatalogIsNotInstalledAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage catalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(revision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateWorkspaceWithMissingOffering(revision);
        Func<CancellationToken, Task<VerifiedCatalogPackage>> download =
            delegate (CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(catalogPackage);
            };
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(workspace),
            download))
        {
            await Assert.ThrowsAsync<ProductWorkspaceCatalogCompatibilityException>(
                async delegate
                {
                    await context.Loader.LoadAsync(CancellationToken.None);
                });
            CatalogCacheLoadResult cacheLoadResult =
                await context.CatalogCacheStore.LoadAsync(CancellationToken.None);

            Assert.False(cacheLoadResult.IsFound);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
            Assert.Equal(1, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task CorruptCacheBlocksDownloaderFallbackAsync()
    {
        VerifiedCatalogPackage catalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(new CatalogRevision(1));
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateNotFound()))
        {
            await context.CatalogCacheStore.SaveAsync(
                catalogPackage,
                CancellationToken.None);
            await context.CorruptOnlyCatalogGenerationAsync();

            await Assert.ThrowsAsync<CatalogCachePersistenceException>(
                async delegate
                {
                    await context.Loader.LoadAsync(CancellationToken.None);
                });

            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task RecoveredWorkspaceGenerationIsReportedAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage catalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(revision);
        PlanningWorkspace workspace =
            ProductWorkspaceLoaderTestData.CreateEmptyWorkspace(revision);
        using (ProductWorkspaceLoaderTestContext context = createContext(
            PlanningWorkspaceLoadResult.CreateRecoveredPreviousGeneration(workspace)))
        {
            await context.CatalogCacheStore.SaveAsync(
                catalogPackage,
                CancellationToken.None);

            ProductWorkspaceLoadResult result = await context.Loader.LoadAsync(
                CancellationToken.None);

            Assert.True(result.WasWorkspaceRecovered);
            Assert.False(result.WasWorkspaceCreated);
            Assert.Same(workspace, result.Workspace);
            Assert.Empty(context.WorkspaceStore.SavedWorkspaces);
        }
    }

    private static ProductWorkspaceLoaderTestContext createContext(
        PlanningWorkspaceLoadResult workspaceLoadResult)
    {
        return new ProductWorkspaceLoaderTestContext(
            workspaceLoadResult,
            Array.Empty<Func<CancellationToken, Task<VerifiedCatalogPackage>>>());
    }

    private static ProductWorkspaceLoaderTestContext createContext(
        PlanningWorkspaceLoadResult workspaceLoadResult,
        Func<CancellationToken, Task<VerifiedCatalogPackage>> download)
    {
        return new ProductWorkspaceLoaderTestContext(
            workspaceLoadResult,
            new Func<CancellationToken, Task<VerifiedCatalogPackage>>[] { download });
    }
}
