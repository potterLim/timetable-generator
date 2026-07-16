using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogJsonFormatException : FormatException
{
    public string JsonPath { get; }

    public CatalogJsonFormatException(string jsonPath, string reason)
        : base(buildMessage(jsonPath, reason))
    {
        JsonPath = jsonPath;
    }

    public CatalogJsonFormatException(
        string jsonPath,
        string reason,
        Exception innerException)
        : base(buildMessage(jsonPath, reason), innerException)
    {
        JsonPath = jsonPath;
    }

    private static string buildMessage(string jsonPath, string reason)
    {
        return "Invalid catalog JSON at " + jsonPath + ": " + reason;
    }
}
