using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogOfferingMetadata
{
    public OfferingId OfferingId { get; }

    public CatalogOfferingClassificationMetadata Classification { get; }

    public CatalogOfferingInstructionMetadata Instruction { get; }

    public CatalogOfferingLogisticsMetadata Logistics { get; }

    public CatalogOfferingCapacityMetadata Capacity { get; }

    public OfferingDetailsMetadata Details { get; }

    public SourceRecordNumber SourceRecordNumber { get; }

    public CatalogOfferingMetadata(
        OfferingId offeringId,
        CatalogOfferingClassificationMetadata classification,
        CatalogOfferingInstructionMetadata instruction,
        CatalogOfferingLogisticsMetadata logistics,
        CatalogOfferingCapacityMetadata capacity,
        OfferingDetailsMetadata details,
        SourceRecordNumber sourceRecordNumber)
    {
        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        if (classification == null)
        {
            throw new ArgumentNullException(nameof(classification));
        }

        if (instruction == null)
        {
            throw new ArgumentNullException(nameof(instruction));
        }

        if (logistics == null)
        {
            throw new ArgumentNullException(nameof(logistics));
        }

        if (capacity == null)
        {
            throw new ArgumentNullException(nameof(capacity));
        }

        if (details == null)
        {
            throw new ArgumentNullException(nameof(details));
        }

        if (sourceRecordNumber.IsValid == false)
        {
            throw new ArgumentException("Catalog offering metadata requires a valid source record number.", nameof(sourceRecordNumber));
        }

        OfferingId = offeringId;
        Classification = classification;
        Instruction = instruction;
        Logistics = logistics;
        Capacity = capacity;
        Details = details;
        SourceRecordNumber = sourceRecordNumber;
    }
}
