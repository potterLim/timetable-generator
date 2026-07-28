using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class CatalogOffering
{
    public CourseOfferingKey Key { get; }

    public OfferingClassification Classification { get; }

    public OfferingInstruction Instruction { get; }

    public OfferingLogistics Logistics { get; }

    public OfferingCapacity Capacity { get; }

    public OfferingDetails Details { get; }

    public SourceRecordNumber SourceRecordNumber { get; }

    public CatalogOffering(
        CourseOfferingKey key,
        OfferingClassification classification,
        OfferingInstruction instruction,
        OfferingLogistics logistics,
        OfferingCapacity capacity,
        OfferingDetails details,
        SourceRecordNumber sourceRecordNumber)
    {
        if (key.CourseCode == null || key.SectionCode == null)
        {
            throw new ArgumentException("Catalog offerings require a valid key.", nameof(key));
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

        if (sourceRecordNumber.Value <= 0)
        {
            throw new ArgumentException("Catalog offerings require a valid source record number.", nameof(sourceRecordNumber));
        }

        Key = key;
        Classification = classification;
        Instruction = instruction;
        Logistics = logistics;
        Capacity = capacity;
        Details = details;
        SourceRecordNumber = sourceRecordNumber;
    }
}
