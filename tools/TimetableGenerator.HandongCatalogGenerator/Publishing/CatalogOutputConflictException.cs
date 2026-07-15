using System;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal sealed class CatalogOutputConflictException : Exception
{
    public string CatalogPath { get; }

    public CatalogOutputConflictException(string catalogPath)
        : base(
            "The immutable catalog revision already exists with different content: "
            + catalogPath)
    {
        CatalogPath = catalogPath;
    }
}
