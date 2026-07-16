using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Product.Loading;

internal interface IProductWorkspaceDataLoader
{
    Task<ProductWorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken);
}
