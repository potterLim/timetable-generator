using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogFileDescriptor
{
    public CatalogRelativePath RelativePath { get; }

    public CatalogMediaType MediaType { get; }

    public CatalogCharset Charset { get; }

    public CatalogContentEncoding ContentEncoding { get; }

    public CatalogFileSize Size { get; }

    public Sha256Digest Sha256 { get; }

    public CatalogFileDescriptor(
        CatalogRelativePath relativePath,
        CatalogMediaType mediaType,
        CatalogCharset charset,
        CatalogContentEncoding contentEncoding,
        CatalogFileSize size,
        Sha256Digest sha256)
    {
        if (relativePath == null)
        {
            throw new ArgumentNullException(nameof(relativePath));
        }

        if (mediaType == null)
        {
            throw new ArgumentNullException(nameof(mediaType));
        }

        if (charset == null)
        {
            throw new ArgumentNullException(nameof(charset));
        }

        if (contentEncoding == null)
        {
            throw new ArgumentNullException(nameof(contentEncoding));
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
