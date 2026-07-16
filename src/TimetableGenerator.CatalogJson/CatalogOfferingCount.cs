using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogOfferingCount
{
    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0;
        }
    }

    public CatalogOfferingCount(int value)
    {
        CatalogCountValidation.requirePositive(value, "Catalog offering counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
