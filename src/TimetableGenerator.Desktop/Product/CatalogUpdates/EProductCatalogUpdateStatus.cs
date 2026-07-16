namespace TimetableGenerator.Desktop.Product.CatalogUpdates;

internal enum EProductCatalogUpdateStatus
{
    Current,
    Staged,
    WorkspaceIncompatible,
    TransitionRejected,
    RevisionArtifactChanged,
}
