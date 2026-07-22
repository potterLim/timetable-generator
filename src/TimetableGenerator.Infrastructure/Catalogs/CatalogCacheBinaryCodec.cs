using System;
using System.Buffers.Binary;
using TimetableGenerator.CatalogJson;

namespace TimetableGenerator.Infrastructure.Catalogs;

internal sealed class CatalogCacheBinaryCodec
{
    private const int CACHE_SCHEMA_VERSION = 1;
    private const int CACHE_SCHEMA_VERSION_OFFSET = 8;
    private const int GENERATION_OFFSET = 12;
    private const int INDEX_LENGTH_OFFSET = 20;
    private const int CATALOG_LENGTH_OFFSET = 24;

    internal const int HEADER_LENGTH = 28;

    private static readonly byte[] CACHE_MAGIC = new byte[]
    {
        0x54,
        0x47,
        0x43,
        0x41,
        0x43,
        0x48,
        0x45,
        0x00,
    };

    private readonly CatalogSynchronizationLimits mLimits;

    public CatalogCacheBinaryCodec(CatalogSynchronizationLimits limits)
    {
        if (limits == null)
        {
            throw new ArgumentNullException(nameof(limits));
        }

        mLimits = limits;
    }

    public byte[] Serialize(CatalogCacheDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        ReadOnlyMemory<byte> indexBytes = document.Package.IndexBytes;
        ReadOnlyMemory<byte> catalogBytes = document.Package.CatalogBytes;
        validateResourceLengths(indexBytes.Length, catalogBytes.Length);
        int contentLength = checked(HEADER_LENGTH + indexBytes.Length + catalogBytes.Length);
        byte[] content = new byte[contentLength];
        CACHE_MAGIC.CopyTo(content, 0);
        BinaryPrimitives.WriteInt32LittleEndian(
            content.AsSpan(CACHE_SCHEMA_VERSION_OFFSET, sizeof(int)),
            CACHE_SCHEMA_VERSION);
        BinaryPrimitives.WriteInt64LittleEndian(
            content.AsSpan(GENERATION_OFFSET, sizeof(long)),
            document.Generation.Value);
        BinaryPrimitives.WriteInt32LittleEndian(
            content.AsSpan(INDEX_LENGTH_OFFSET, sizeof(int)),
            indexBytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(
            content.AsSpan(CATALOG_LENGTH_OFFSET, sizeof(int)),
            catalogBytes.Length);
        indexBytes.Span.CopyTo(content.AsSpan(HEADER_LENGTH, indexBytes.Length));
        catalogBytes.Span.CopyTo(content.AsSpan(HEADER_LENGTH + indexBytes.Length, catalogBytes.Length));
        return content;
    }

    public CatalogCacheDocument Deserialize(ReadOnlyMemory<byte> content)
    {
        if (content.Length < HEADER_LENGTH)
        {
            throw new CatalogCacheDocumentException(
                "The catalog cache document is shorter than its required header.");
        }

        ReadOnlySpan<byte> contentSpan = content.Span;
        if (contentSpan.Slice(0, CACHE_MAGIC.Length).SequenceEqual(CACHE_MAGIC) == false)
        {
            throw new CatalogCacheDocumentException(
                "The catalog cache document has an invalid file signature.");
        }

        int schemaVersion = BinaryPrimitives.ReadInt32LittleEndian(
            contentSpan.Slice(CACHE_SCHEMA_VERSION_OFFSET, sizeof(int)));
        if (schemaVersion > CACHE_SCHEMA_VERSION)
        {
            throw new UnsupportedCatalogCacheSchemaVersionException(schemaVersion);
        }

        if (schemaVersion != CACHE_SCHEMA_VERSION)
        {
            throw new CatalogCacheDocumentException(
                "The catalog cache document has an invalid schema version.");
        }

        long generationValue = BinaryPrimitives.ReadInt64LittleEndian(
            contentSpan.Slice(GENERATION_OFFSET, sizeof(long)));
        if (generationValue <= 0L)
        {
            throw new CatalogCacheDocumentException("The catalog cache document has an invalid generation.");
        }

        int indexLength = BinaryPrimitives.ReadInt32LittleEndian(
            contentSpan.Slice(INDEX_LENGTH_OFFSET, sizeof(int)));
        int catalogLength = BinaryPrimitives.ReadInt32LittleEndian(
            contentSpan.Slice(CATALOG_LENGTH_OFFSET, sizeof(int)));
        validateResourceLengths(indexLength, catalogLength);
        long expectedLength = HEADER_LENGTH + (long)indexLength + catalogLength;
        if (expectedLength != content.Length)
        {
            throw new CatalogCacheDocumentException(
                "The catalog cache document length does not match its header.");
        }

        ReadOnlyMemory<byte> indexBytes = content.Slice(HEADER_LENGTH, indexLength);
        ReadOnlyMemory<byte> catalogBytes = content.Slice(HEADER_LENGTH + indexLength, catalogLength);
        try
        {
            VerifiedCatalogPackage package = VerifiedCatalogPackage.ReadAndVerify(indexBytes, catalogBytes);
            return new CatalogCacheDocument(new CatalogCacheGeneration(generationValue), package);
        }
        catch (CatalogJsonFormatException exception)
        {
            throw new CatalogCacheDocumentException(
                "The catalog cache contains an invalid verified package.",
                exception);
        }
    }

    private void validateResourceLengths(int indexLength, int catalogLength)
    {
        if (indexLength <= 0 || indexLength > mLimits.Index.Bytes)
        {
            throw new CatalogCacheDocumentException("The cached index exceeds its configured size limit.");
        }

        if (catalogLength <= 0 || catalogLength > mLimits.Catalog.Bytes)
        {
            throw new CatalogCacheDocumentException("The cached catalog exceeds its configured size limit.");
        }
    }
}
