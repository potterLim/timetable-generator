using System;
using System.IO;

namespace TimetableGenerator.Desktop.Storage;

internal sealed record ProductDataRootPath
{
    private const string PRODUCT_DIRECTORY_NAME = "TimetableGenerator";

    public string Value { get; }

    public ProductDataRootPath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Product data paths cannot be empty.", nameof(value));
        }

        string fullPath = Path.GetFullPath(value);
        if (Path.IsPathFullyQualified(fullPath) == false)
        {
            throw new ArgumentException("Product data paths must be fully qualified.", nameof(value));
        }

        Value = fullPath;
    }

    public static ProductDataRootPath CreateDefault()
    {
        string localApplicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationDataPath))
        {
            throw new InvalidOperationException(
                "The operating system did not provide a local application data path.");
        }

        return new ProductDataRootPath(Path.Combine(localApplicationDataPath, PRODUCT_DIRECTORY_NAME));
    }

    public override string ToString()
    {
        return Value;
    }
}
