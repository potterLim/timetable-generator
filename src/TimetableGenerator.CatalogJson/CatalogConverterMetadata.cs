using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogConverterMetadata
{
    public CatalogConverterId Id { get; }

    public CatalogConverterVersion Version { get; }

    public CatalogConverterMetadata(CatalogConverterId id, CatalogConverterVersion version)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (version == null)
        {
            throw new ArgumentNullException(nameof(version));
        }

        Id = id;
        Version = version;
    }
}
