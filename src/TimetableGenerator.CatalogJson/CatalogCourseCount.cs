using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogCourseCount
{
    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0;
        }
    }

    public CatalogCourseCount(int value)
    {
        CatalogCountValidation.requirePositive(value, "Catalog course counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
