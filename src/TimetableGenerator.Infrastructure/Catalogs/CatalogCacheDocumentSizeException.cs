namespace TimetableGenerator.Infrastructure.Catalogs;

internal sealed class CatalogCacheDocumentSizeException : CatalogCacheDocumentException
{
    public CatalogCacheDocumentSizeException(string message)
        : base(message)
    {
    }
}
