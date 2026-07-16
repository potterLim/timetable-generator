using System;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed class RemoteCatalogSynchronizationException : Exception
{
    public ERemoteCatalogSynchronizationFailureKind FailureKind { get; }

    public RemoteCatalogSynchronizationException(
        ERemoteCatalogSynchronizationFailureKind failureKind,
        string message)
        : base(message)
    {
        validateFailureKind(failureKind);
        FailureKind = failureKind;
    }

    public RemoteCatalogSynchronizationException(
        ERemoteCatalogSynchronizationFailureKind failureKind,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        validateFailureKind(failureKind);
        FailureKind = failureKind;
    }

    private static void validateFailureKind(
        ERemoteCatalogSynchronizationFailureKind failureKind)
    {
        if (Enum.IsDefined(typeof(ERemoteCatalogSynchronizationFailureKind), failureKind)
            == false)
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind));
        }
    }
}
