using System;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Configuration;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Product.Loading;

internal sealed class ConfiguredProductCatalogDownloader : IProductCatalogDownloader
{
    private readonly CatalogSourceConfigurationLoader mConfigurationLoader;

    private readonly CatalogSynchronizationLimits mSynchronizationLimits;

    private readonly CatalogCacheFileStore mCacheStore;

    public ConfiguredProductCatalogDownloader(CatalogSourceConfigurationLoader configurationLoader, CatalogSynchronizationLimits synchronizationLimits, CatalogCacheFileStore cacheStore)
    {
        if (configurationLoader == null)
        {
            throw new ArgumentNullException(nameof(configurationLoader));
        }

        if (synchronizationLimits == null)
        {
            throw new ArgumentNullException(nameof(synchronizationLimits));
        }

        if (cacheStore == null)
        {
            throw new ArgumentNullException(nameof(cacheStore));
        }

        mConfigurationLoader = configurationLoader;
        mSynchronizationLimits = synchronizationLimits;
        mCacheStore = cacheStore;
    }

    public async Task<VerifiedCatalogPackage> DownloadDefaultCatalogAsync(CancellationToken cancellationToken)
    {
        CatalogSourceConfiguration configuration = await mConfigurationLoader.LoadAsync(cancellationToken).ConfigureAwait(false);
        using (RemoteCatalogSynchronizer synchronizer = RemoteCatalogSynchronizer.Create(configuration.Endpoint, mSynchronizationLimits, mCacheStore))
        {
            return await synchronizer.DownloadDefaultCatalogAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
