namespace TimetableGenerator.CatalogJson;

public sealed class GradingMetadata
{
    public EGradingType Type { get; }

    public EPassFailOptionAvailability PassFailOptionAvailability { get; }

    public bool IsPassFailOptionAvailable
    {
        get
        {
            return PassFailOptionAvailability == EPassFailOptionAvailability.Available;
        }
    }

    public GradingMetadata(
        EGradingType type,
        EPassFailOptionAvailability passFailOptionAvailability)
    {
        Type = type;
        PassFailOptionAvailability = passFailOptionAvailability;
    }
}
