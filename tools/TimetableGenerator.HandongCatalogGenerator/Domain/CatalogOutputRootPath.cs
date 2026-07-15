using System;
using System.IO;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed record CatalogOutputRootPath
{
    public string Value { get; }

    public CatalogOutputRootPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The output root path cannot be empty.", nameof(value));
        }

        Value = Path.GetFullPath(value.Trim());
    }

    public override string ToString()
    {
        return Value;
    }
}
