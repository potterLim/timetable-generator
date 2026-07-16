using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogMultiInstructorDisplayCount
{
    public int Value { get; }

    public CatalogMultiInstructorDisplayCount(int value)
    {
        CatalogCountValidation.requireNonNegative(value, "Multi-instructor display counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
