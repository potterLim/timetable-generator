using System;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogConverterVersion
{
    public Version Value { get; }

    public CatalogConverterVersion(Version value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
