using System;
using System.IO;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed record CatalogCacheFilePath
{
    public string Value { get; }

    public CatalogCacheFilePath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Catalog cache paths cannot be empty.", nameof(value));
        }

        string fullPath = Path.GetFullPath(value);
        string fileName = Path.GetFileName(fullPath);
        string baseFileName = Path.GetFileNameWithoutExtension(fullPath);
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(baseFileName))
        {
            throw new ArgumentException("Catalog cache paths must identify a named file.", nameof(value));
        }

        Value = fullPath;
    }

    public override string ToString()
    {
        return Value;
    }
}
