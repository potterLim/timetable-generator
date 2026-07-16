using System;

namespace TimetableGenerator.CatalogJson;

public sealed record OfferingUnitName
{
    public string Value { get; }

    public OfferingUnitName(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Offering unit names cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
