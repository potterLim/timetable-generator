using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogEnrollmentNotProvidedCount
{
    public int Value { get; }

    public CatalogEnrollmentNotProvidedCount(int value)
    {
        CatalogCountValidation.requireNonNegative(value, "Enrollment-not-provided counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
