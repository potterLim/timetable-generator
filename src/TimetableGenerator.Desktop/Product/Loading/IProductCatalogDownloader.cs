using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Product.Loading;

internal interface IProductCatalogDownloader
{
    Task<VerifiedCatalogPackage> DownloadDefaultCatalogAsync(
        CancellationToken cancellationToken);
}
