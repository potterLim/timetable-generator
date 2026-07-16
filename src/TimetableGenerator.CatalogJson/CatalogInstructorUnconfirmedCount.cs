using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogInstructorUnconfirmedCount
{
    public int Value { get; }

    public CatalogInstructorUnconfirmedCount(int value)
    {
        CatalogCountValidation.requireNonNegative(value, "Instructor-unconfirmed counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
