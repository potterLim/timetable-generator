using System;
using TimetableGenerator.HandongCatalogGenerator.Publishing;

namespace TimetableGenerator.HandongCatalogGenerator.Application;

internal sealed record CatalogGenerationResult
{
    public CatalogArtifactPath CatalogPath { get; }
    public CatalogArtifactPath IndexPath { get; }
    public CatalogFileSize CatalogFileSize { get; }
    public CatalogFileSize IndexFileSize { get; }
    public Sha256Digest CatalogSha256 { get; }
    public Sha256Digest SourceSha256 { get; }
    public CatalogGenerationSummary Summary { get; }

    public CatalogGenerationResult(
        CatalogArtifactPath catalogPath,
        CatalogArtifactPath indexPath,
        CatalogFileSize catalogFileSize,
        CatalogFileSize indexFileSize,
        Sha256Digest catalogSha256,
        Sha256Digest sourceSha256,
        CatalogGenerationSummary summary)
    {
        ArgumentNullException.ThrowIfNull(catalogPath);
        ArgumentNullException.ThrowIfNull(indexPath);
        ArgumentNullException.ThrowIfNull(summary);

        CatalogPath = catalogPath;
        IndexPath = indexPath;
        CatalogFileSize = catalogFileSize;
        IndexFileSize = indexFileSize;
        CatalogSha256 = catalogSha256;
        SourceSha256 = sourceSha256;
        Summary = summary;
    }
}
