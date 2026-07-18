using System;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Presentation.Catalog;

internal sealed class CatalogOfferingProjection
{
    public CatalogOffering Offering { get; }

    public CatalogOfferingMetadata Metadata { get; }

    public EnglishInstructionPercentage EnglishInstructionPercentage { get; }

    public string InstructorSummary { get; }

    public string LocationSummary { get; }

    public string ScheduleSummary { get; }

    public CatalogOfferingProjection(
        CatalogOffering offering,
        CatalogOfferingMetadata metadata)
    {
        if (offering == null)
        {
            throw new ArgumentNullException(nameof(offering));
        }

        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }

        if (offering.Id != metadata.OfferingId)
        {
            throw new ArgumentException(
                "Offering metadata must describe the projected catalog offering.",
                nameof(metadata));
        }

        bool hasProvidedSchedule = offering.MeetingSchedule.IsScheduled;
        if (hasProvidedSchedule != metadata.Logistics.HasScheduleSourceText)
        {
            throw new ArgumentException(
                "Offering schedule metadata must match the domain schedule status.",
                nameof(metadata));
        }

        Offering = offering;
        Metadata = metadata;
        EnglishInstructionPercentage =
            metadata.Instruction.EnglishInstructionPercentage;
        InstructorSummary = CatalogSummaryFormatter.FormatInstructorSummary(
            metadata.Instruction.InstructorAssignment);
        LocationSummary = CatalogSummaryFormatter.FormatLocationSummary(
            metadata.Logistics.Location);
        ScheduleSummary = CatalogSummaryFormatter.FormatScheduleSummary(
            offering.MeetingSchedule);
    }
}
