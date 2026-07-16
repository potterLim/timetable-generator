using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Presentation.ViewModels;

namespace TimetableGenerator.Desktop.Product;

internal interface IProductWorkspaceLoader
{
    Task<PlannerWorkspaceViewModel> LoadAsync(CancellationToken cancellationToken);
}
