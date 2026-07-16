using System.Threading;
using System.Threading.Tasks;
namespace TimetableGenerator.Desktop.Product;

internal interface IProductWorkspaceLoader
{
    Task<ProductWorkspacePresentation> LoadAsync(CancellationToken cancellationToken);
}
