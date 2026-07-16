using System;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed class RemoteCatalogSynchronizationException : Exception
{
    public RemoteCatalogSynchronizationException(string message)
        : base(message)
    {
    }

    public RemoteCatalogSynchronizationException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
