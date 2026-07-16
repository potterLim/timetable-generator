using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogScheduledOfferingCount
{
    public int Value { get; }

    public CatalogScheduledOfferingCount(int value)
    {
        CatalogCountValidation.requireNonNegative(value, "Scheduled offering counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
