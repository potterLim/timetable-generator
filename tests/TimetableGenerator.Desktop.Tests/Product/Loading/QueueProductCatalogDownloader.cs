using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Tests.Product.Loading;

internal sealed class QueueProductCatalogDownloader : IProductCatalogDownloader
{
    private readonly Queue<Func<CancellationToken, Task<VerifiedCatalogPackage>>> mDownloads;

    public int DownloadCount { get; private set; }

    public QueueProductCatalogDownloader(
        IEnumerable<Func<CancellationToken, Task<VerifiedCatalogPackage>>> downloads)
    {
        if (downloads == null)
        {
            throw new ArgumentNullException(nameof(downloads));
        }

        mDownloads = new Queue<Func<CancellationToken, Task<VerifiedCatalogPackage>>>(downloads);
    }

    public Task<VerifiedCatalogPackage> DownloadDefaultCatalogAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ++DownloadCount;
        if (mDownloads.Count == 0)
        {
            throw new InvalidOperationException("The test catalog downloader received an unexpected request.");
        }

        Func<CancellationToken, Task<VerifiedCatalogPackage>> download = mDownloads.Dequeue();
        return download(cancellationToken);
    }
}
