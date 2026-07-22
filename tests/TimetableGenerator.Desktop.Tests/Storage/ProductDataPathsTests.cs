using System;
using System.IO;
using Avalonia.Headless.XUnit;
using TimetableGenerator.Desktop.Storage;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Storage;

public sealed class ProductDataPathsTests
{
    [AvaloniaFact]
    public void ProductPathsKeepDurableStateInSeparateDirectories()
    {
        string rootPath = Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Tests",
            Guid.NewGuid().ToString("N"));
        ProductDataPaths paths = new ProductDataPaths(new ProductDataRootPath(rootPath));

        Assert.Equal(Path.Combine(rootPath, "Catalogs", "catalog-cache-v1.bin"), paths.CatalogCache.Value);
        Assert.Equal(Path.Combine(rootPath, "Planning", "workspace-v1.json"), paths.Workspace.Value);
        Assert.Equal(
            Path.Combine(rootPath, "Settings", "appearance-v1.json"),
            paths.AppearanceSettings.Value);
        Assert.Equal(Path.GetFullPath(rootPath), paths.Root.Value);
    }
}
