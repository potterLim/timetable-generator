using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class OfferingDetails
{
    public ESyllabusAvailability SyllabusAvailability { get; }

    public ERemarksAvailability RemarksAvailability { get; }

    public bool AreRemarksAvailable
    {
        get
        {
            return RemarksAvailability == ERemarksAvailability.LookupAvailable;
        }
    }

    public OfferingDetails(ESyllabusAvailability syllabusAvailability, ERemarksAvailability remarksAvailability)
    {
        if (Enum.IsDefined(typeof(ESyllabusAvailability), syllabusAvailability) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(syllabusAvailability));
        }

        if (Enum.IsDefined(typeof(ERemarksAvailability), remarksAvailability) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(remarksAvailability));
        }

        SyllabusAvailability = syllabusAvailability;
        RemarksAvailability = remarksAvailability;
    }
}
