using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogSourceEnglishScheduleMismatchCount
{
    public int Value { get; }

    public CatalogSourceEnglishScheduleMismatchCount(int value)
    {
        CatalogCountValidation.requireNonNegative(value, "Source English schedule mismatch counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
