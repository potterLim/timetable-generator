namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal sealed record CatalogPackageWriteResult
{
    public string CatalogPath { get; }
    public string IndexPath { get; }
    public CatalogFileSize CatalogFileSize { get; }
    public CatalogFileSize IndexFileSize { get; }
    public Sha256Digest CatalogSha256 { get; }

    public CatalogPackageWriteResult(
        string catalogPath,
        string indexPath,
        CatalogFileSize catalogFileSize,
        CatalogFileSize indexFileSize,
        Sha256Digest catalogSha256)
    {
        CatalogPath = catalogPath;
        IndexPath = indexPath;
        CatalogFileSize = catalogFileSize;
        IndexFileSize = indexFileSize;
        CatalogSha256 = catalogSha256;
    }
}
