using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Desktop.Tests.Product.Loading;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Product;

public sealed class ProductWorkspaceViewModelLoaderTests
{
    [Fact]
    public async Task LoadPreservesRecoveryFlagsInPresentationAsync()
    {
        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage catalogPackage = ProductWorkspaceLoaderTestData.CreateCatalogPackage(revision);
        PlanningWorkspace workspace = ProductWorkspaceLoaderTestData.CreateEmptyWorkspace(revision);
        PlanningWorkspaceLoadResult persistedWorkspaceLoadResult =
            PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(
                workspace,
                new PlanningWorkspaceConcurrencyToken(1L));
        RecordingPlanningWorkspaceStore workspaceStore = new RecordingPlanningWorkspaceStore(persistedWorkspaceLoadResult);
        EProductWorkspaceRecoveryFlags recoveryFlags =
            EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration
            | EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration;
        ProductWorkspaceLoadResult dataLoadResult =
            new ProductWorkspaceLoadResult(
                catalogPackage,
                workspace,
                workspaceStore,
                persistedWorkspaceLoadResult.ConcurrencyToken,
                EProductCatalogOrigin.OfflineCache,
                recoveryFlags);
        FixedProductWorkspaceDataLoader dataLoader = new FixedProductWorkspaceDataLoader(dataLoadResult);
        ProductWorkspaceViewModelLoader loader = new ProductWorkspaceViewModelLoader(dataLoader);

        ProductWorkspacePresentation presentation = await loader.LoadAsync(CancellationToken.None);

        try
        {
            Assert.Equal(recoveryFlags, presentation.RecoveryFlags);
        }
        finally
        {
            presentation.Workspace.Dispose();
        }
    }
}
