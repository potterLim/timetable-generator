using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogMeetingNotProvidedCount
{
    public int Value { get; }

    public CatalogMeetingNotProvidedCount(int value)
    {
        CatalogCountValidation.requireNonNegative(value, "Meeting-not-provided counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
