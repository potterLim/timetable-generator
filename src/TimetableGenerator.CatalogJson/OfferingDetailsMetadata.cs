namespace TimetableGenerator.CatalogJson;

public sealed class OfferingDetailsMetadata
{
    public ERemarksAvailability RemarksAvailability { get; }

    public bool AreRemarksAvailable
    {
        get
        {
            return RemarksAvailability == ERemarksAvailability.Available;
        }
    }

    public OfferingDetailsMetadata(ERemarksAvailability remarksAvailability)
    {
        RemarksAvailability = remarksAvailability;
    }
}
