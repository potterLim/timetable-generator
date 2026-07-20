using System;
using System.IO;

using TimetableGenerator.Infrastructure.Catalogs;
using TimetableGenerator.Infrastructure.Persistence;

namespace TimetableGenerator.Desktop.Storage;

internal sealed class ProductDataPaths
{
    private const string CATALOG_CACHE_FILE_NAME = "catalog-cache-v1.bin";
    private const string WORKSPACE_FILE_NAME = "workspace-v1.json";

    private const string APPEARANCE_SETTINGS_FILE_NAME = "appearance-v1.json";

    public ProductDataRootPath Root { get; }

    public CatalogCacheFilePath CatalogCache { get; }

    public WorkspaceFilePath Workspace { get; }

    public ProductAppearanceSettingsFilePath AppearanceSettings { get; }

    public ProductDataPaths(ProductDataRootPath root)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        Root = root;
        CatalogCache = new CatalogCacheFilePath(
            Path.Combine(root.Value, "Catalogs", CATALOG_CACHE_FILE_NAME));
        Workspace = new WorkspaceFilePath(
            Path.Combine(root.Value, "Planning", WORKSPACE_FILE_NAME));
        AppearanceSettings = new ProductAppearanceSettingsFilePath(
            Path.Combine(
                root.Value,
                "Settings",
                APPEARANCE_SETTINGS_FILE_NAME));
    }
}
