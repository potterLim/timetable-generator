using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogOfferingInstructionMetadata
{
    public InstructorAssignmentMetadata InstructorAssignment { get; }

    public EnglishInstructionPercentage EnglishInstructionPercentage { get; }

    public GradingMetadata Grading { get; }

    public CatalogOfferingInstructionMetadata(InstructorAssignmentMetadata instructorAssignment, EnglishInstructionPercentage englishInstructionPercentage, GradingMetadata grading)
    {
        if (instructorAssignment == null)
        {
            throw new ArgumentNullException(nameof(instructorAssignment));
        }

        if (grading == null)
        {
            throw new ArgumentNullException(nameof(grading));
        }

        InstructorAssignment = instructorAssignment;
        EnglishInstructionPercentage = englishInstructionPercentage;
        Grading = grading;
    }
}
