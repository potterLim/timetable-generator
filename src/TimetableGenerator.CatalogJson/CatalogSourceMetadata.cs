using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogSourceMetadata
{
    public InstitutionId ProviderId { get; }

    public CatalogSourceLogicalFileName LogicalFileName { get; }

    public CatalogFileExtension DeclaredExtension { get; }

    public CatalogMediaType DetectedMediaType { get; }

    public CatalogCharset DeclaredCharset { get; }

    public CatalogDecoderName DecodedWith { get; }

    public CatalogFileSize Size { get; }

    public Sha256Digest Sha256 { get; }

    public CatalogSourceMetadata(
        InstitutionId providerId,
        CatalogSourceLogicalFileName logicalFileName,
        CatalogFileExtension declaredExtension,
        CatalogMediaType detectedMediaType,
        CatalogCharset declaredCharset,
        CatalogDecoderName decodedWith,
        CatalogFileSize size,
        Sha256Digest sha256)
    {
        if (providerId == null)
        {
            throw new ArgumentNullException(nameof(providerId));
        }

        if (logicalFileName == null)
        {
            throw new ArgumentNullException(nameof(logicalFileName));
        }

        if (declaredExtension == null)
        {
            throw new ArgumentNullException(nameof(declaredExtension));
        }

        if (detectedMediaType == null)
        {
            throw new ArgumentNullException(nameof(detectedMediaType));
        }

        if (declaredCharset == null)
        {
            throw new ArgumentNullException(nameof(declaredCharset));
        }

        if (decodedWith == null)
        {
            throw new ArgumentNullException(nameof(decodedWith));
        }

        if (size.IsValid == false)
        {
            throw new ArgumentException("Catalog source metadata requires a valid positive size.", nameof(size));
        }

        if (sha256 == null)
        {
            throw new ArgumentNullException(nameof(sha256));
        }

        ProviderId = providerId;
        LogicalFileName = logicalFileName;
        DeclaredExtension = declaredExtension;
        DetectedMediaType = detectedMediaType;
        DeclaredCharset = declaredCharset;
        DecodedWith = decodedWith;
        Size = size;
        Sha256 = sha256;
    }
}
