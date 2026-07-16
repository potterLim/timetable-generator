using System;
using System.IO;

namespace TimetableGenerator.Desktop.Storage;

internal sealed record ProductAppearanceSettingsFilePath
{
    public string Value { get; }

    public ProductAppearanceSettingsFilePath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Appearance settings file paths cannot be empty.",
                nameof(value));
        }

        if (Path.IsPathFullyQualified(value) == false)
        {
            throw new ArgumentException(
                "Appearance settings file paths must be fully qualified.",
                nameof(value));
        }

        string fullPath = Path.GetFullPath(value);
        if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)))
        {
            throw new ArgumentException(
                "Appearance settings paths must identify a file.",
                nameof(value));
        }

        Value = fullPath;
    }

    public override string ToString()
    {
        return Value;
    }
}
