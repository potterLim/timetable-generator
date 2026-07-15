using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class OfferingClassification
{
    public ERequirementType RequirementType { get; }

    public OfferingUnitName OfferingUnitName { get; }

    public EInstructionSession InstructionSession { get; }

    public GeneralEducationCategoryAssignment GeneralEducationCategory { get; }

    public OfferingClassification(
        ERequirementType requirementType,
        OfferingUnitName offeringUnitName,
        EInstructionSession instructionSession,
        GeneralEducationCategoryAssignment generalEducationCategory)
    {
        if (Enum.IsDefined(typeof(ERequirementType), requirementType) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(requirementType));
        }

        if (offeringUnitName == null)
        {
            throw new ArgumentNullException(nameof(offeringUnitName));
        }

        if (Enum.IsDefined(typeof(EInstructionSession), instructionSession) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionSession));
        }

        if (generalEducationCategory == null)
        {
            throw new ArgumentNullException(nameof(generalEducationCategory));
        }

        RequirementType = requirementType;
        OfferingUnitName = offeringUnitName;
        InstructionSession = instructionSession;
        GeneralEducationCategory = generalEducationCategory;
    }
}
