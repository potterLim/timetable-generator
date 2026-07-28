using System;
using System.IO;

namespace TimetableGenerator.CatalogJson;

public sealed record CatalogRelativePath
{
    private const char PATH_SEPARATOR = '/';

    public string Value { get; }

    public CatalogRelativePath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        validate(value);
        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }

    private static void validate(string value)
    {
        if (value.Length == 0)
        {
            throw new ArgumentException("Catalog relative paths cannot be empty.", nameof(value));
        }

        bool isAbsoluteUri = Uri.TryCreate(value, UriKind.Absolute, out _);
        if (Path.IsPathRooted(value)
            || value.StartsWith(PATH_SEPARATOR)
            || value.StartsWith("//", StringComparison.Ordinal)
            || value.Contains('\\')
            || value.Contains('?')
            || value.Contains('#')
            || isAbsoluteUri)
        {
            throw new ArgumentException("Catalog paths must be plain relative paths without an origin, query, or fragment.", nameof(value));
        }

        string[] segments = value.Split(PATH_SEPARATOR);
        foreach (string segment in segments)
        {
            validateSegment(segment, value);
        }
    }

    private static void validateSegment(string segment, string value)
    {
        if (segment.Length == 0)
        {
            throw new ArgumentException("Catalog paths cannot contain empty path segments.", nameof(value));
        }

        string decodedSegment;
        try
        {
            decodedSegment = Uri.UnescapeDataString(segment);
        }
        catch (UriFormatException exception)
        {
            throw new ArgumentException("Catalog paths cannot contain malformed escape sequences.", nameof(value), exception);
        }

        bool isDotSegment = string.Equals(decodedSegment, ".", StringComparison.Ordinal) || string.Equals(decodedSegment, "..", StringComparison.Ordinal);
        bool escapesSegment = decodedSegment.Contains(PATH_SEPARATOR) || decodedSegment.Contains('\\');
        if (isDotSegment || escapesSegment)
        {
            throw new ArgumentException("Catalog paths cannot contain dot segments or encoded path separators.", nameof(value));
        }
    }
}
