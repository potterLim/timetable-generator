using System;
using TimetableGenerator.CatalogJson;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed class VerifiedCatalogPackage
{
    private readonly byte[] mIndexBytes;

    private readonly byte[] mCatalogBytes;

    public CatalogIndexDocument Index { get; }

    public CatalogIndexEntry Entry { get; }

    public CourseCatalogDocument Document { get; }

    internal ReadOnlyMemory<byte> IndexBytes
    {
        get
        {
            return mIndexBytes;
        }
    }

    internal ReadOnlyMemory<byte> CatalogBytes
    {
        get
        {
            return mCatalogBytes;
        }
    }

    private VerifiedCatalogPackage(
        byte[] indexBytes,
        byte[] catalogBytes,
        CatalogIndexDocument index,
        CatalogIndexEntry entry,
        CourseCatalogDocument document)
    {
        mIndexBytes = indexBytes;
        mCatalogBytes = catalogBytes;
        Index = index;
        Entry = entry;
        Document = document;
    }

    public static VerifiedCatalogPackage ReadAndVerify(
        ReadOnlyMemory<byte> indexBytes,
        ReadOnlyMemory<byte> catalogBytes)
    {
        CatalogIndexDocument index = CatalogIndexJsonReader.Read(indexBytes);
        CatalogIndexEntry entry = index.FindDefaultEntry();
        CourseCatalogDocument document = CourseCatalogJsonReader.ReadAndVerify(
            catalogBytes,
            entry);
        return new VerifiedCatalogPackage(
            indexBytes.ToArray(),
            catalogBytes.ToArray(),
            index,
            entry,
            document);
    }
}
