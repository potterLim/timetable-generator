using System;

using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Product;

internal sealed class ProductWorkspacePresentation
{
    private const EProductWorkspaceRecoveryFlags ALL_RECOVERY_FLAGS =
        EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration
        | EProductWorkspaceRecoveryFlags.WorkspacePreviousGeneration
        | EProductWorkspaceRecoveryFlags.WorkspaceCatalogRebound
        | EProductWorkspaceRecoveryFlags.WorkspaceCreated;

    public PlannerWorkspaceViewModel Workspace { get; }

    public VerifiedCatalogPackage ActiveCatalogPackage { get; }

    public PlanningWorkspace WorkspaceSnapshot { get; }

    public EProductCatalogOrigin CatalogOrigin { get; }

    public EProductWorkspaceRecoveryFlags RecoveryFlags { get; }

    public ProductWorkspacePresentation(
        PlannerWorkspaceViewModel workspace,
        VerifiedCatalogPackage activeCatalogPackage,
        PlanningWorkspace workspaceSnapshot,
        EProductCatalogOrigin catalogOrigin,
        EProductWorkspaceRecoveryFlags recoveryFlags)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        if (activeCatalogPackage == null)
        {
            throw new ArgumentNullException(nameof(activeCatalogPackage));
        }

        if (workspaceSnapshot == null)
        {
            throw new ArgumentNullException(nameof(workspaceSnapshot));
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

        bool isRemoteWithRecoveredCatalog =
            catalogOrigin == EProductCatalogOrigin.RemoteDownload
            && (recoveryFlags
                & EProductWorkspaceRecoveryFlags.CatalogPreviousGeneration) != 0;
        if (isRemoteWithRecoveredCatalog)
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

        Workspace = workspace;
        ActiveCatalogPackage = activeCatalogPackage;
        WorkspaceSnapshot = workspaceSnapshot;
        CatalogOrigin = catalogOrigin;
        RecoveryFlags = recoveryFlags;
    }
}
