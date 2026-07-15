using System;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class HandongOfferingInformationNormalizationResult
{
    public OfferingUnitName OfferingUnitName { get; }

    public EInstructionSession InstructionSession { get; }

    public InstructorAssignment InstructorAssignment { get; }

    public HandongOfferingInformationNormalizationResult(
        OfferingUnitName offeringUnitName,
        EInstructionSession instructionSession,
        InstructorAssignment instructorAssignment)
    {
        if (offeringUnitName == null)
        {
            throw new ArgumentNullException(nameof(offeringUnitName));
        }

        if (Enum.IsDefined(typeof(EInstructionSession), instructionSession) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(instructionSession));
        }

        if (instructorAssignment == null)
        {
            throw new ArgumentNullException(nameof(instructorAssignment));
        }

        OfferingUnitName = offeringUnitName;
        InstructionSession = instructionSession;
        InstructorAssignment = instructorAssignment;
    }
}
