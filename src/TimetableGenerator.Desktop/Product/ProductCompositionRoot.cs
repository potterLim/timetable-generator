using System;

using TimetableGenerator.Desktop.Configuration;
using TimetableGenerator.Desktop.Product.CatalogUpdates;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Infrastructure.Catalogs;
using TimetableGenerator.Infrastructure.Persistence;

namespace TimetableGenerator.Desktop.Product;

internal static class ProductCompositionRoot
{
    public static ProductShellViewModel CreateShell(
        ProductDataPaths dataPaths,
        CatalogSourceConfigurationPath configurationPath)
    {
        if (dataPaths == null)
        {
            throw new ArgumentNullException(nameof(dataPaths));
        }

        if (configurationPath == null)
        {
            throw new ArgumentNullException(nameof(configurationPath));
        }

        CatalogSynchronizationLimits synchronizationLimits =
            ProductCatalogSynchronizationDefaults.CreateLimits();
        CatalogCacheFileStore catalogCacheStore = new CatalogCacheFileStore(
            dataPaths.CatalogCache,
            synchronizationLimits);
        PlanningWorkspaceFileStore workspaceStore = new PlanningWorkspaceFileStore(
            dataPaths.Workspace,
            new PlanningWorkspaceJsonCodec(),
            WorkspaceDocumentSizeLimit.ProductDefault);
        CatalogSourceConfigurationLoader configurationLoader =
            new CatalogSourceConfigurationLoader(configurationPath);
        ConfiguredProductCatalogDownloader catalogDownloader =
            new ConfiguredProductCatalogDownloader(
                configurationLoader,
                synchronizationLimits,
                catalogCacheStore);
        ProductWorkspaceLoader dataLoader = new ProductWorkspaceLoader(
            catalogCacheStore,
            workspaceStore,
            catalogDownloader);
        ProductWorkspaceViewModelLoader viewModelLoader =
            new ProductWorkspaceViewModelLoader(dataLoader);
        ProductCatalogUpdateService catalogUpdateService =
            new ProductCatalogUpdateService(
                catalogDownloader,
                catalogCacheStore);
        return new ProductShellViewModel(
            viewModelLoader,
            catalogUpdateService);
    }
}
