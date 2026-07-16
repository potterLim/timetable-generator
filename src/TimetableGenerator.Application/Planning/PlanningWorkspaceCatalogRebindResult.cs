using System;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public sealed class PlanningWorkspaceCatalogRebindResult
{
    public EPlanningWorkspaceCatalogRebindStatus Status { get; }

    public PlanningWorkspace? ReboundWorkspaceOrNull { get; }

    public bool IsRebound
    {
        get
        {
            return Status == EPlanningWorkspaceCatalogRebindStatus.Rebound;
        }
    }

    private PlanningWorkspaceCatalogRebindResult(
        EPlanningWorkspaceCatalogRebindStatus status,
        PlanningWorkspace? reboundWorkspaceOrNull)
    {
        if (Enum.IsDefined(typeof(EPlanningWorkspaceCatalogRebindStatus), status) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        bool hasReboundWorkspace = reboundWorkspaceOrNull != null;
        if ((status == EPlanningWorkspaceCatalogRebindStatus.Rebound)
            != hasReboundWorkspace)
        {
            throw new ArgumentException(
                "Only a successful catalog rebind can contain a workspace.",
                nameof(reboundWorkspaceOrNull));
        }

        Status = status;
        ReboundWorkspaceOrNull = reboundWorkspaceOrNull;
    }

    internal static PlanningWorkspaceCatalogRebindResult createRebound(
        PlanningWorkspace reboundWorkspace)
    {
        if (reboundWorkspace == null)
        {
            throw new ArgumentNullException(nameof(reboundWorkspace));
        }

        return new PlanningWorkspaceCatalogRebindResult(
            EPlanningWorkspaceCatalogRebindStatus.Rebound,
            reboundWorkspace);
    }

    internal static PlanningWorkspaceCatalogRebindResult createFailure(
        EPlanningWorkspaceCatalogRebindStatus status)
    {
        if (status == EPlanningWorkspaceCatalogRebindStatus.Rebound)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return new PlanningWorkspaceCatalogRebindResult(status, null);
    }
}
