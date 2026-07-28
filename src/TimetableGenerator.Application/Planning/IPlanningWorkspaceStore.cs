using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public interface IPlanningWorkspaceStore
{
    Task<PlanningWorkspaceLoadResult> LoadAsync(CancellationToken cancellationToken);

    Task<PlanningWorkspaceConcurrencyToken> SaveAsync(PlanningWorkspace workspace, PlanningWorkspaceConcurrencyToken expectedToken, CancellationToken cancellationToken);
}
