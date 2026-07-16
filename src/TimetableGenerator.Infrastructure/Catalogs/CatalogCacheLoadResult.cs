using System;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed class CatalogCacheLoadResult
{
    public ECatalogCacheLoadStatus Status { get; }

    public VerifiedCatalogPackage? PackageOrNull { get; }

    public bool IsFound
    {
        get
        {
            return Status != ECatalogCacheLoadStatus.NotFound;
        }
    }

    private CatalogCacheLoadResult(
        ECatalogCacheLoadStatus status,
        VerifiedCatalogPackage? packageOrNull)
    {
        if (status == ECatalogCacheLoadStatus.NotFound && packageOrNull != null)
        {
            throw new ArgumentException(
                "A not-found cache result cannot contain a catalog package.",
                nameof(packageOrNull));
        }

        if (status != ECatalogCacheLoadStatus.NotFound && packageOrNull == null)
        {
            throw new ArgumentNullException(nameof(packageOrNull));
        }

        Status = status;
        PackageOrNull = packageOrNull;
    }

    public VerifiedCatalogPackage GetPackage()
    {
        if (PackageOrNull == null)
        {
            throw new InvalidOperationException("The cache result does not contain a catalog.");
        }

        return PackageOrNull;
    }

    internal static CatalogCacheLoadResult createNotFound()
    {
        return new CatalogCacheLoadResult(ECatalogCacheLoadStatus.NotFound, null);
    }

    internal static CatalogCacheLoadResult createLoadedLatestGeneration(
        VerifiedCatalogPackage package)
    {
        return new CatalogCacheLoadResult(
            ECatalogCacheLoadStatus.LoadedLatestGeneration,
            package);
    }

    internal static CatalogCacheLoadResult createRecoveredPreviousGeneration(
        VerifiedCatalogPackage package)
    {
        return new CatalogCacheLoadResult(
            ECatalogCacheLoadStatus.RecoveredPreviousGeneration,
            package);
    }
}
