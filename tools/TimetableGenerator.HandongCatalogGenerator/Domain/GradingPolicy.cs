using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class GradingPolicy
{
    public EGradingType GradingType { get; }

    public EPassFailOptionAvailability PassFailOptionAvailability { get; }

    public bool IsPassFailOptionAvailable
    {
        get
        {
            return PassFailOptionAvailability == EPassFailOptionAvailability.Available;
        }
    }

    public GradingPolicy(
        EGradingType gradingType,
        EPassFailOptionAvailability passFailOptionAvailability)
    {
        if (Enum.IsDefined(typeof(EGradingType), gradingType) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(gradingType));
        }

        if (Enum.IsDefined(typeof(EPassFailOptionAvailability), passFailOptionAvailability) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(passFailOptionAvailability));
        }

        GradingType = gradingType;
        PassFailOptionAvailability = passFailOptionAvailability;
    }
}
