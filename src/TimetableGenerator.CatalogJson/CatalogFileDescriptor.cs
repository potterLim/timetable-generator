using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogFileDescriptor
{
    public CatalogRelativePath RelativePath { get; }

    public string MediaType { get; }

    public string Charset { get; }

    public string ContentEncoding { get; }

    public CatalogFileSize Size { get; }

    public Sha256Digest Sha256 { get; }

    public CatalogFileDescriptor(
        CatalogRelativePath relativePath,
        string mediaType,
        string charset,
        string contentEncoding,
        CatalogFileSize size,
        Sha256Digest sha256)
    {
        if (relativePath == null)
        {
            throw new ArgumentNullException(nameof(relativePath));
        }

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException("Media types cannot be empty.", nameof(mediaType));
        }

        if (string.IsNullOrWhiteSpace(charset))
        {
            throw new ArgumentException("Charsets cannot be empty.", nameof(charset));
        }

        if (string.IsNullOrWhiteSpace(contentEncoding))
        {
            throw new ArgumentException(
                "Content encodings cannot be empty.",
                nameof(contentEncoding));
        }

        if (sha256 == null)
        {
            throw new ArgumentNullException(nameof(sha256));
        }

        if (size.IsValid == false)
        {
            throw new ArgumentException(
                "Catalog file descriptors require a valid positive size.",
                nameof(size));
        }

        RelativePath = relativePath;
        MediaType = mediaType;
        Charset = charset;
        ContentEncoding = contentEncoding;
        Size = size;
        Sha256 = sha256;
    }

    public bool HasExpectedContent(ReadOnlySpan<byte> content)
    {
        return content.Length == Size.Value && Sha256.Matches(content);
    }
}
