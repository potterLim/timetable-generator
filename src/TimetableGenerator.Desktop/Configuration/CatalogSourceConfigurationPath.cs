using System;
using System.IO;

namespace TimetableGenerator.Desktop.Configuration;

internal sealed record CatalogSourceConfigurationPath
{
    public string Value { get; }

    public CatalogSourceConfigurationPath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Catalog source configuration paths cannot be empty.", nameof(value));
        }

        string fullPath = Path.GetFullPath(value);
        if (string.Equals(
            Path.GetExtension(fullPath),
            ".json",
            StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new ArgumentException(
                "Catalog source configuration paths must identify a JSON file.",
                nameof(value));
        }

        Value = fullPath;
    }

    public override string ToString()
    {
        return Value;
    }
}
