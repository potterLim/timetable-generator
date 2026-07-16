using System;

namespace TimetableGenerator.Infrastructure.Catalogs;

public class CatalogCachePersistenceException : Exception
{
    public CatalogCachePersistenceException(string message)
        : base(message)
    {
    }

    public CatalogCachePersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
