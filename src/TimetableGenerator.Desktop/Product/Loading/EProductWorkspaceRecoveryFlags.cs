using System;

namespace TimetableGenerator.Desktop.Product.Loading;

[Flags]
internal enum EProductWorkspaceRecoveryFlags
{
    None = 0,
    CatalogPreviousGeneration = 1 << 0,
    WorkspacePreviousGeneration = 1 << 1,
    WorkspaceCatalogRebound = 1 << 2,
    WorkspaceCreated = 1 << 3,
}
