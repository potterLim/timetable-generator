using System;
using System.IO;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed record CatalogSourceFilePath
{
    public string Value { get; }

    public CatalogSourceFilePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The source file path cannot be empty.", nameof(value));
        }

        Value = Path.GetFullPath(value.Trim());
    }

    public override string ToString()
    {
        return Value;
    }
}
