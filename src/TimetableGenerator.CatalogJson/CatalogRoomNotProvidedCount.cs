using System.Globalization;
using TimetableGenerator.CatalogJson.Internal;

namespace TimetableGenerator.CatalogJson;

public readonly record struct CatalogRoomNotProvidedCount
{
    public int Value { get; }

    public CatalogRoomNotProvidedCount(int value)
    {
        CatalogCountValidation.requireNonNegative(value, "Room-not-provided counts");
        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
