using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogSourceRemarkLookupOnlyCount
{
    public int Value { get; }

    public CatalogSourceRemarkLookupOnlyCount(int value)
    {
        CatalogCountValidation.requireNonNegative(value, "Source remark lookup-only counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
