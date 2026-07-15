using System;
using System.IO;

namespace TimetableGenerator.HandongCatalogGenerator.Application;

internal sealed record CatalogArtifactPath
{
    public string Value { get; }

    public CatalogArtifactPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A catalog artifact path cannot be empty.", nameof(value));
        }

        Value = Path.GetFullPath(value.Trim());
    }

    public override string ToString()
    {
        return Value;
    }
}
