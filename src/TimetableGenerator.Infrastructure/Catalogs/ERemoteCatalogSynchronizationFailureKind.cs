namespace TimetableGenerator.Infrastructure.Catalogs;

public enum ERemoteCatalogSynchronizationFailureKind
{
    Network,
    InvalidRemoteData,
    ResourceLimit,
    SecurityPolicy,
    LocalPersistence,
}
