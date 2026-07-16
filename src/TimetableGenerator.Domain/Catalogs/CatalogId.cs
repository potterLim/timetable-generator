using System;

namespace TimetableGenerator.Domain.Catalogs;

public sealed record CatalogId
{
    public string Value { get; }

    public CatalogId(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0)
        {
            throw new ArgumentException("Catalog IDs cannot be empty.", nameof(value));
        }

        Value = normalizedValue;
    }

    public override string ToString()
    {
        return Value;
    }
}
