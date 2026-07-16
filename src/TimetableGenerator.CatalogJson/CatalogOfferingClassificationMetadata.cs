using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogOfferingClassificationMetadata
{
    private readonly GeneralEducationCategoryName? mGeneralEducationCategoryOrNull;

    public ERequirementType RequirementType { get; }

    public OfferingUnitName OfferingUnitName { get; }

    public EInstructionSession InstructionSession { get; }

    public bool HasGeneralEducationCategory
    {
        get
        {
            return mGeneralEducationCategoryOrNull != null;
        }
    }

    private CatalogOfferingClassificationMetadata(
        ERequirementType requirementType,
        OfferingUnitName offeringUnitName,
        EInstructionSession instructionSession,
        GeneralEducationCategoryName? generalEducationCategoryOrNull)
    {
        if (offeringUnitName == null)
        {
            throw new ArgumentNullException(nameof(offeringUnitName));
        }

        RequirementType = requirementType;
        OfferingUnitName = offeringUnitName;
        InstructionSession = instructionSession;
        mGeneralEducationCategoryOrNull = generalEducationCategoryOrNull;
    }

    public static CatalogOfferingClassificationMetadata CreateWithoutGeneralEducationCategory(
        ERequirementType requirementType,
        OfferingUnitName offeringUnitName,
        EInstructionSession instructionSession)
    {
        return new CatalogOfferingClassificationMetadata(
            requirementType,
            offeringUnitName,
            instructionSession,
            null);
    }

    public static CatalogOfferingClassificationMetadata CreateWithGeneralEducationCategory(
        ERequirementType requirementType,
        OfferingUnitName offeringUnitName,
        EInstructionSession instructionSession,
        GeneralEducationCategoryName generalEducationCategory)
    {
        if (generalEducationCategory == null)
        {
            throw new ArgumentNullException(nameof(generalEducationCategory));
        }

        return new CatalogOfferingClassificationMetadata(
            requirementType,
            offeringUnitName,
            instructionSession,
            generalEducationCategory);
    }

    public GeneralEducationCategoryName GetGeneralEducationCategory()
    {
        if (mGeneralEducationCategoryOrNull == null)
        {
            throw new InvalidOperationException("No general education category is available.");
        }

        return mGeneralEducationCategoryOrNull;
    }
}
