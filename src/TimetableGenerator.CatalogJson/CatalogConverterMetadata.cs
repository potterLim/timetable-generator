using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogConverterMetadata
{
    public string Id { get; }

    public string Version { get; }

    public CatalogConverterMetadata(string id, string version)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Converter IDs cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Converter versions cannot be empty.", nameof(version));
        }

        Id = id;
        Version = version;
    }
}
