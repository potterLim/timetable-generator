using System;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Product.Loading;

internal sealed class ProductWorkspaceLoadResult
{
    private const EProductWorkspaceRecoveryFlags ALL_RECOVERY_FLAGS =
        EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration
        | EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration
        | EProductWorkspaceRecoveryFlags.WorkspaceCatalogRebound
        | EProductWorkspaceRecoveryFlags.WorkspaceCreated;

    public VerifiedCatalogPackage CatalogPackage { get; }

    public PlanningWorkspace Workspace { get; }

    public IPlanningWorkspaceStore WorkspaceStore { get; }

    public EProductCatalogOrigin CatalogOrigin { get; }

    public EProductWorkspaceRecoveryFlags RecoveryFlags { get; }

    public bool WasCatalogCacheRecovered
    {
        get
        {
            return (RecoveryFlags
                & EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration) != 0;
        }
    }

    public bool WasWorkspaceRecovered
    {
        get
        {
            return (RecoveryFlags
                & EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration) != 0;
        }
    }

    public bool WasWorkspaceCatalogRebound
    {
        get
        {
            return (RecoveryFlags
                & EProductWorkspaceRecoveryFlags.WorkspaceCatalogRebound) != 0;
        }
    }

    public bool WasWorkspaceCreated
    {
        get
        {
            return (RecoveryFlags
                & EProductWorkspaceRecoveryFlags.WorkspaceCreated) != 0;
        }
    }

    internal ProductWorkspaceLoadResult(
        VerifiedCatalogPackage catalogPackage,
        PlanningWorkspace workspace,
        IPlanningWorkspaceStore workspaceStore,
        EProductCatalogOrigin catalogOrigin,
        EProductWorkspaceRecoveryFlags recoveryFlags)
    {
        if (catalogPackage == null)
        {
            throw new ArgumentNullException(nameof(catalogPackage));
        }

        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (workspaceStore == null)
        {
            throw new ArgumentNullException(nameof(workspaceStore));
        }

        if (Enum.IsDefined(typeof(EProductCatalogOrigin), catalogOrigin) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(catalogOrigin));
        }

        EProductWorkspaceRecoveryFlags unknownFlags = recoveryFlags & ~ALL_RECOVERY_FLAGS;
        if (unknownFlags != EProductWorkspaceRecoveryFlags.None)
        {
            throw new ArgumentOutOfRangeException(nameof(recoveryFlags));
        }

        bool isRemoteWithRecoveredCache =
            catalogOrigin == EProductCatalogOrigin.RemoteDownload
            && (recoveryFlags
                & EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration) != 0;
        if (isRemoteWithRecoveredCache)
        {
            throw new ArgumentException(
                "A remotely downloaded catalog cannot be a recovered cache generation.",
                nameof(recoveryFlags));
        }

        bool isCreatedAndRecovered =
            (recoveryFlags & EProductWorkspaceRecoveryFlags.WorkspaceCreated) != 0
            && (recoveryFlags
                & EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration) != 0;
        bool isCreatedAndRebound =
            (recoveryFlags & EProductWorkspaceRecoveryFlags.WorkspaceCreated) != 0
            && (recoveryFlags
                & EProductWorkspaceRecoveryFlags.WorkspaceCatalogRebound) != 0;
        if (isCreatedAndRecovered || isCreatedAndRebound)
        {
            throw new ArgumentException(
                "A newly created workspace cannot also be recovered or rebound.",
                nameof(recoveryFlags));
        }

        requireWorkspaceBindingMatchesCatalog(catalogPackage, workspace);

        CatalogPackage = catalogPackage;
        Workspace = workspace;
        WorkspaceStore = workspaceStore;
        CatalogOrigin = catalogOrigin;
        RecoveryFlags = recoveryFlags;
    }

    private static void requireWorkspaceBindingMatchesCatalog(
        VerifiedCatalogPackage catalogPackage,
        PlanningWorkspace workspace)
    {
        PlanCatalogBinding packageBinding = catalogPackage.CreatePlanCatalogBinding();
        if (workspace.CatalogBinding != packageBinding)
        {
            throw new ArgumentException(
                "The loaded workspace must be bound to the loaded catalog.",
                nameof(workspace));
        }

        foreach (PlanningPlan plan in workspace.Plans)
        {
            PlanCatalogBinding binding = plan.CatalogBinding;
            if (binding != packageBinding)
            {
                throw new ArgumentException(
                    "Every loaded plan must be bound to the loaded catalog.",
                    nameof(workspace));
            }
        }
    }
}
