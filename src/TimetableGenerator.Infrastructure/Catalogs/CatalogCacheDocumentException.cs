using System;

namespace TimetableGenerator.Infrastructure.Catalogs;

internal class CatalogCacheDocumentException : Exception
{
    public CatalogCacheDocumentException(string message)
        : base(message)
    {
    }

    public CatalogCacheDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
