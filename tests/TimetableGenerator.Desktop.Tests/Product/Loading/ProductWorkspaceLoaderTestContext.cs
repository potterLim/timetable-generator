using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Tests.Product.Loading;

internal sealed class ProductWorkspaceLoaderTestContext : IDisposable
{
    private const long MAXIMUM_CATALOG_BYTES = 1_000_000L;
    private const long MAXIMUM_INDEX_BYTES = 64_000L;

    private readonly string mDirectoryPath;

    public CatalogCacheFileStore CatalogCacheStore { get; }

    public RecordingPlanningWorkspaceStore WorkspaceStore { get; }

    public QueueProductCatalogDownloader CatalogDownloader { get; }

    public ProductWorkspaceLoader Loader { get; }

    public ProductWorkspaceLoaderTestContext(
        PlanningWorkspaceLoadResult workspaceLoadResult,
        IEnumerable<Func<CancellationToken, Task<VerifiedCatalogPackage>>> downloads)
    {
        if (workspaceLoadResult == null)
        {
            throw new ArgumentNullException(nameof(workspaceLoadResult));
        }

        if (downloads == null)
        {
            throw new ArgumentNullException(nameof(downloads));
        }

        mDirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Desktop.Tests",
            Guid.NewGuid().ToString("N"));
        CatalogSynchronizationLimits limits = new CatalogSynchronizationLimits(
            new CatalogResourceByteLimit(MAXIMUM_INDEX_BYTES),
            new CatalogResourceByteLimit(MAXIMUM_CATALOG_BYTES));
        CatalogCacheFilePath cachePath = new CatalogCacheFilePath(
            Path.Combine(mDirectoryPath, "catalog-cache.bin"));
        CatalogCacheStore = new CatalogCacheFileStore(cachePath, limits);
        WorkspaceStore = new RecordingPlanningWorkspaceStore(workspaceLoadResult);
        CatalogDownloader = new QueueProductCatalogDownloader(downloads);
        Loader = new ProductWorkspaceLoader(
            CatalogCacheStore,
            WorkspaceStore,
            CatalogDownloader);
    }

    public void Dispose()
    {
        if (Directory.Exists(mDirectoryPath))
        {
            Directory.Delete(mDirectoryPath, true);
        }
    }

    public async Task CorruptOnlyCatalogGenerationAsync()
    {
        string generationPath = Path.Combine(
            mDirectoryPath,
            "catalog-cache.g00000000000000000001.bin");
        await File.WriteAllBytesAsync(
            generationPath,
            new byte[] { 0x01, 0x02, 0x03 },
            CancellationToken.None);
    }
}
