using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Product.CatalogUpdates;
using TimetableGenerator.Desktop.Tests.Product.Loading;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Product.CatalogUpdates;

public sealed class ProductCatalogUpdateServiceTests
{
    [Fact]
    public async Task CompatibleForwardRevisionIsStagedWithoutChangingWorkspaceAsync()
    {
        CatalogRevision activeRevision = new CatalogRevision(1);
        CatalogRevision candidateRevision = new CatalogRevision(2);
        VerifiedCatalogPackage activePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(activeRevision);
        VerifiedCatalogPackage candidatePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(candidateRevision);
        PlanningWorkspace workspace = ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(activeRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(candidatePackage))
        {
            await context.CatalogCacheStore.SaveAsync(activePackage, CancellationToken.None);
            ProductCatalogUpdateService service = new ProductCatalogUpdateService(context.CatalogDownloader, context.CatalogCacheStore);

            ProductCatalogUpdateResult result = await service.CheckAndStageAsync(activePackage, workspace, CancellationToken.None);
            CatalogCacheLoadResult latestLoad = await context.CatalogCacheStore.LoadAsync(CancellationToken.None);
            CatalogCacheLoadResult protectedLoad = await context.CatalogCacheStore.LoadMatchingAsync(workspace.Plans[0].CatalogBinding, CancellationToken.None);

            Assert.Equal(EProductCatalogUpdateStatus.Staged, result.Status);
            Assert.Equal(candidateRevision, result.CandidateRevision);
            Assert.Equal(candidateRevision, latestLoad.GetPackage().Entry.Revision);
            Assert.Equal(activeRevision, protectedLoad.GetPackage().Entry.Revision);
            Assert.Equal(activeRevision, workspace.Plans[0].CatalogBinding.Revision);
        }
    }

    [Fact]
    public async Task WorkspaceWithoutPlansCanStageACompatibleForwardRevisionAsync()
    {
        CatalogRevision activeRevision = new CatalogRevision(1);
        CatalogRevision candidateRevision = new CatalogRevision(2);
        VerifiedCatalogPackage activePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(activeRevision);
        VerifiedCatalogPackage candidatePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(candidateRevision);
        PlanningWorkspace workspace = ProductWorkspaceLoaderTestData.CreateWorkspaceWithoutPlans(activeRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(candidatePackage))
        {
            ProductCatalogUpdateService service = new ProductCatalogUpdateService(context.CatalogDownloader, context.CatalogCacheStore);

            ProductCatalogUpdateResult result = await service.CheckAndStageAsync(activePackage, workspace, CancellationToken.None);
            CatalogCacheLoadResult latestLoad = await context.CatalogCacheStore.LoadAsync(CancellationToken.None);

            Assert.Equal(EProductCatalogUpdateStatus.Staged, result.Status);
            Assert.Equal(candidateRevision, result.CandidateRevision);
            Assert.Equal(candidateRevision, latestLoad.GetPackage().Entry.Revision);
            Assert.Empty(workspace.Plans);
            Assert.Null(workspace.ActivePlanIdOrNull);
            Assert.Equal(activeRevision, workspace.CatalogBinding.Revision);
        }
    }

    [Fact]
    public async Task CurrentArtifactDoesNotCreateAReplacementGenerationAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage activePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(revision);
        PlanningWorkspace workspace = ProductWorkspaceLoaderTestData.CreateEmptyWorkspace(revision);
        using (ProductWorkspaceLoaderTestContext context = createContext(activePackage))
        {
            await context.CatalogCacheStore.SaveAsync(activePackage, CancellationToken.None);
            ProductCatalogUpdateService service = new ProductCatalogUpdateService(context.CatalogDownloader, context.CatalogCacheStore);

            ProductCatalogUpdateResult result = await service.CheckAndStageAsync(activePackage, workspace, CancellationToken.None);

            Assert.Equal(EProductCatalogUpdateStatus.Current, result.Status);
        }
    }

    [Fact]
    public async Task ReusedRevisionWithDifferentArtifactIsRejectedAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage activePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(revision);
        VerifiedCatalogPackage changedPackage = ProductWorkspaceLoaderTestData.CreateCatalogPackageWithoutSavedCourse(revision);
        PlanningWorkspace workspace = ProductWorkspaceLoaderTestData.CreateEmptyWorkspace(revision);
        using (ProductWorkspaceLoaderTestContext context = createContext(changedPackage))
        {
            ProductCatalogUpdateService service = new ProductCatalogUpdateService(context.CatalogDownloader, context.CatalogCacheStore);

            ProductCatalogUpdateResult result = await service.CheckAndStageAsync(activePackage, workspace, CancellationToken.None);

            Assert.Equal(EProductCatalogUpdateStatus.RevisionArtifactChanged, result.Status);
            CatalogCacheLoadResult cacheLoad = await context.CatalogCacheStore.LoadAsync(CancellationToken.None);
            Assert.False(cacheLoad.IsFound);
        }
    }

    [Fact]
    public async Task ActivePackageWithChangedArtifactIsRejectedBeforeDownloadAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage changedActivePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackageWithoutSavedCourse(revision);
        VerifiedCatalogPackage candidatePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(new CatalogRevision(2));
        PlanningWorkspace workspace = ProductWorkspaceLoaderTestData.CreateEmptyWorkspace(revision);
        using (ProductWorkspaceLoaderTestContext context = createContext(candidatePackage))
        {
            ProductCatalogUpdateService service = new ProductCatalogUpdateService(context.CatalogDownloader, context.CatalogCacheStore);

            await Assert.ThrowsAsync<ArgumentException>(
                async delegate
                {
                    await service.CheckAndStageAsync(changedActivePackage, workspace, CancellationToken.None);
                });

            Assert.Equal(0, context.CatalogDownloader.DownloadCount);
        }
    }

    [Fact]
    public async Task IncompatibleForwardRevisionIsNotStagedAsync()
    {
        CatalogRevision activeRevision = new CatalogRevision(1);
        CatalogRevision candidateRevision = new CatalogRevision(2);
        VerifiedCatalogPackage activePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(activeRevision);
        VerifiedCatalogPackage candidatePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackageWithoutSavedCourse(candidateRevision);
        PlanningWorkspace workspace = ProductWorkspaceLoaderTestData.CreateWorkspaceWithValidSelection(activeRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(candidatePackage))
        {
            ProductCatalogUpdateService service = new ProductCatalogUpdateService(context.CatalogDownloader, context.CatalogCacheStore);

            ProductCatalogUpdateResult result = await service.CheckAndStageAsync(activePackage, workspace, CancellationToken.None);

            Assert.Equal(EProductCatalogUpdateStatus.WorkspaceIncompatible, result.Status);
            CatalogCacheLoadResult cacheLoad = await context.CatalogCacheStore.LoadAsync(CancellationToken.None);
            Assert.False(cacheLoad.IsFound);
        }
    }

    [Fact]
    public async Task RevisionRollbackIsNotStagedAsync()
    {
        CatalogRevision activeRevision = new CatalogRevision(2);
        CatalogRevision candidateRevision = new CatalogRevision(1);
        VerifiedCatalogPackage activePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(activeRevision);
        VerifiedCatalogPackage candidatePackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(candidateRevision);
        PlanningWorkspace workspace = ProductWorkspaceLoaderTestData.CreateEmptyWorkspace(activeRevision);
        using (ProductWorkspaceLoaderTestContext context = createContext(candidatePackage))
        {
            ProductCatalogUpdateService service = new ProductCatalogUpdateService(context.CatalogDownloader, context.CatalogCacheStore);

            ProductCatalogUpdateResult result = await service.CheckAndStageAsync(activePackage, workspace, CancellationToken.None);

            Assert.Equal(EProductCatalogUpdateStatus.TransitionRejected, result.Status);
            CatalogCacheLoadResult cacheLoad = await context.CatalogCacheStore.LoadAsync(CancellationToken.None);
            Assert.False(cacheLoad.IsFound);
        }
    }

    private static ProductWorkspaceLoaderTestContext createContext(VerifiedCatalogPackage candidatePackage)
    {
        return new ProductWorkspaceLoaderTestContext(
            PlanningWorkspaceLoadResult.CreateNotFound(),
            new Func<CancellationToken, Task<VerifiedCatalogPackage>>[]
            {
                delegate
                {
                    return Task.FromResult(candidatePackage);
                },
            });
    }
}
