namespace TimetableGenerator.Infrastructure.Catalogs;

internal sealed class UnsupportedCatalogCacheSchemaVersionException :
    CatalogCacheDocumentException
{
    public int SchemaVersion { get; }

    public UnsupportedCatalogCacheSchemaVersionException(int schemaVersion)
        : base("Unsupported catalog cache schema version: " + schemaVersion + ".")
    {
        SchemaVersion = schemaVersion;
    }
}
