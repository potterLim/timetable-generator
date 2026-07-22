using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Desktop.Storage;

namespace TimetableGenerator.Desktop.Product;

internal sealed class ProductWorkspaceViewModelLoader : IProductWorkspaceLoader
{
    private readonly IProductWorkspaceDataLoader mDataLoader;

    public ProductWorkspaceViewModelLoader(IProductWorkspaceDataLoader dataLoader)
    {
        if (dataLoader == null)
        {
            throw new ArgumentNullException(nameof(dataLoader));
        }

        mDataLoader = dataLoader;
    }

    public async Task<ProductWorkspacePresentation> LoadAsync(CancellationToken cancellationToken)
    {
        ProductWorkspaceLoadResult loadResult = await mDataLoader.LoadAsync(cancellationToken);
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(loadResult.CatalogPackage.Document);
        PlanningWorkspaceSession session = new PlanningWorkspaceSession(
            loadResult.CatalogPackage.Document.Catalog,
            loadResult.Workspace);
        PlanningWorkspaceAutosaveQueue autosaveQueue =
            new PlanningWorkspaceAutosaveQueue(
                loadResult.WorkspaceStore,
                loadResult.WorkspaceConcurrencyToken);
        IScheduleRecommendationProvider recommendationProvider = new CatalogScheduleRecommendationProvider(loadResult.CatalogPackage.Document.Catalog);
        PlannerWorkspaceViewModel workspace = new PlannerWorkspaceViewModel(
            catalogProjection,
            session,
            autosaveQueue,
            recommendationProvider);
        return new ProductWorkspacePresentation(
            workspace,
            loadResult.CatalogPackage,
            loadResult.Workspace,
            loadResult.CatalogOrigin,
            loadResult.RecoveryFlags);
    }
}
