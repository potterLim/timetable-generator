using System;

namespace TimetableGenerator.Infrastructure.Catalogs;

internal sealed class CatalogCacheGenerationFile
{
    public CatalogCacheGeneration Generation { get; }

    public string Path { get; }

    public CatalogCacheGenerationFile(
        CatalogCacheGeneration generation,
        string path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        Generation = generation;
        Path = path;
    }
}
