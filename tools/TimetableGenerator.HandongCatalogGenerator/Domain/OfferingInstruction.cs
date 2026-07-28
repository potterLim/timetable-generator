using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class OfferingInstruction
{
    public InstructorAssignment InstructorAssignment { get; }

    public EnglishInstructionPercentage EnglishInstructionPercentage { get; }

    public GradingPolicy GradingPolicy { get; }

    public OfferingInstruction(InstructorAssignment instructorAssignment, EnglishInstructionPercentage englishInstructionPercentage, GradingPolicy gradingPolicy)
    {
        if (instructorAssignment == null)
        {
            throw new ArgumentNullException(nameof(instructorAssignment));
        }

        if (gradingPolicy == null)
        {
            throw new ArgumentNullException(nameof(gradingPolicy));
        }

        InstructorAssignment = instructorAssignment;
        EnglishInstructionPercentage = englishInstructionPercentage;
        GradingPolicy = gradingPolicy;
    }
}
