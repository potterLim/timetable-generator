using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public sealed class CourseCatalogDocument
{
    private readonly IReadOnlyList<CatalogOfferingMetadata> mOfferingMetadata;

    private readonly IReadOnlyDictionary<OfferingId, CatalogOfferingMetadata> mOfferingMetadataById;

    public CourseCatalog Catalog { get; }

    public InstitutionMetadata Institution { get; }

    public CatalogSourceMetadata Source { get; }

    public CatalogConverterMetadata Converter { get; }

    public CatalogDocumentCounts Counts { get; }

    public CatalogDataQualityMetadata DataQuality { get; }

    public IReadOnlyList<CatalogOfferingMetadata> OfferingMetadata
    {
        get
        {
            return mOfferingMetadata;
        }
    }

    public CourseCatalogDocument(
        CourseCatalog catalog,
        InstitutionMetadata institution,
        CatalogSourceMetadata source,
        CatalogConverterMetadata converter,
        CatalogDocumentCounts counts,
        CatalogDataQualityMetadata dataQuality,
        IEnumerable<CatalogOfferingMetadata> offeringMetadata)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        if (institution == null)
        {
            throw new ArgumentNullException(nameof(institution));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (converter == null)
        {
            throw new ArgumentNullException(nameof(converter));
        }

        if (counts == null)
        {
            throw new ArgumentNullException(nameof(counts));
        }

        if (dataQuality == null)
        {
            throw new ArgumentNullException(nameof(dataQuality));
        }

        if (offeringMetadata == null)
        {
            throw new ArgumentNullException(nameof(offeringMetadata));
        }

        Dictionary<OfferingId, CatalogOfferingMetadata> metadataById = copyAndValidateOfferingMetadata(catalog, offeringMetadata);
        List<CatalogOfferingMetadata> copiedMetadata = new List<CatalogOfferingMetadata>();
        foreach (CatalogOffering offering in catalog.Offerings)
        {
            copiedMetadata.Add(metadataById[offering.Id]);
        }

        Catalog = catalog;
        Institution = institution;
        Source = source;
        Converter = converter;
        Counts = counts;
        DataQuality = dataQuality;
        mOfferingMetadata = copiedMetadata.AsReadOnly();
        mOfferingMetadataById = metadataById;
    }

    public CatalogOfferingMetadata FindOfferingMetadataById(OfferingId offeringId)
    {
        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        CatalogOfferingMetadata? metadataOrNull;
        bool hasMetadata = mOfferingMetadataById.TryGetValue(offeringId, out metadataOrNull);
        if (hasMetadata == false || metadataOrNull == null)
        {
            throw new KeyNotFoundException("No catalog offering metadata exists for " + offeringId + ".");
        }

        return metadataOrNull;
    }

    private static Dictionary<OfferingId, CatalogOfferingMetadata> copyAndValidateOfferingMetadata(
        CourseCatalog catalog,
        IEnumerable<CatalogOfferingMetadata> offeringMetadata)
    {
        HashSet<OfferingId> knownOfferingIds = new HashSet<OfferingId>();
        foreach (CatalogOffering offering in catalog.Offerings)
        {
            knownOfferingIds.Add(offering.Id);
        }

        Dictionary<OfferingId, CatalogOfferingMetadata> metadataById = new Dictionary<OfferingId, CatalogOfferingMetadata>();
        foreach (CatalogOfferingMetadata metadata in offeringMetadata)
        {
            if (metadata == null)
            {
                throw new ArgumentException(
                    "Course catalog documents cannot contain null offering metadata.",
                    nameof(offeringMetadata));
            }

            if (knownOfferingIds.Contains(metadata.OfferingId) == false)
            {
                throw new ArgumentException(
                    "Offering metadata must reference an offering in the domain catalog.",
                    nameof(offeringMetadata));
            }

            if (metadataById.TryAdd(metadata.OfferingId, metadata) == false)
            {
                throw new ArgumentException(
                    "Course catalog documents cannot contain duplicate offering metadata.",
                    nameof(offeringMetadata));
            }
        }

        if (metadataById.Count != knownOfferingIds.Count)
        {
            throw new ArgumentException(
                "Every domain catalog offering requires preserved JSON metadata.",
                nameof(offeringMetadata));
        }

        return metadataById;
    }
}
