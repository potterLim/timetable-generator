using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogSourceMetadata
{
    public InstitutionId ProviderId { get; }

    public string LogicalFileName { get; }

    public string DeclaredExtension { get; }

    public string DetectedMediaType { get; }

    public string DeclaredCharset { get; }

    public string DecodedWith { get; }

    public CatalogFileSize Size { get; }

    public Sha256Digest Sha256 { get; }

    public CatalogSourceMetadata(
        InstitutionId providerId,
        string logicalFileName,
        string declaredExtension,
        string detectedMediaType,
        string declaredCharset,
        string decodedWith,
        CatalogFileSize size,
        Sha256Digest sha256)
    {
        if (providerId == null)
        {
            throw new ArgumentNullException(nameof(providerId));
        }

        validateText(logicalFileName, nameof(logicalFileName));
        validateText(declaredExtension, nameof(declaredExtension));
        validateText(detectedMediaType, nameof(detectedMediaType));
        validateText(declaredCharset, nameof(declaredCharset));
        validateText(decodedWith, nameof(decodedWith));
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

    private static void validateText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Catalog source values cannot be empty.", parameterName);
        }
    }
}
