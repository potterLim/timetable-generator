using System;
using System.Collections.Generic;
using System.Text.Json;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.Desktop.Configuration;

internal static class CatalogSourceConfigurationJsonReader
{
    private const int SUPPORTED_SCHEMA_VERSION = 1;

    private static readonly HashSet<string> EXPECTED_PROPERTY_NAMES =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion",
            "indexUri",
        };

    public static CatalogSourceConfiguration Read(ReadOnlyMemory<byte> jsonBytes)
    {
        if (jsonBytes.IsEmpty)
        {
            throw new CatalogSourceConfigurationException("The catalog source configuration is empty.");
        }

        try
        {
            JsonDocumentOptions options = new JsonDocumentOptions();
            options.AllowTrailingCommas = false;
            options.CommentHandling = JsonCommentHandling.Disallow;
            options.MaxDepth = 8;
            using (JsonDocument document = JsonDocument.Parse(jsonBytes, options))
            {
                return readDocument(document.RootElement);
            }
        }
        catch (CatalogSourceConfigurationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CatalogSourceConfigurationException("The catalog source configuration is not valid UTF-8 JSON.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new CatalogSourceConfigurationException("The catalog index address is invalid.", exception);
        }
    }

    private static CatalogSourceConfiguration readDocument(JsonElement rootElement)
    {
        if (rootElement.ValueKind != JsonValueKind.Object)
        {
            throw new CatalogSourceConfigurationException("The catalog source configuration root must be an object.");
        }

        HashSet<string> discoveredPropertyNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in rootElement.EnumerateObject())
        {
            if (EXPECTED_PROPERTY_NAMES.Contains(property.Name) == false)
            {
                throw new CatalogSourceConfigurationException("The catalog source configuration contains an unknown property: " + property.Name + ".");
            }

            if (discoveredPropertyNames.Add(property.Name) == false)
            {
                throw new CatalogSourceConfigurationException("The catalog source configuration contains a duplicate property: " + property.Name + ".");
            }
        }

        if (discoveredPropertyNames.SetEquals(EXPECTED_PROPERTY_NAMES) == false)
        {
            throw new CatalogSourceConfigurationException("The catalog source configuration is missing a required property.");
        }

        JsonElement schemaVersionElement = rootElement.GetProperty("schemaVersion");
        int schemaVersion;
        if (schemaVersionElement.ValueKind != JsonValueKind.Number
            || schemaVersionElement.TryGetInt32(out schemaVersion) == false
            || schemaVersion != SUPPORTED_SCHEMA_VERSION)
        {
            throw new CatalogSourceConfigurationException("The catalog source configuration schema is not supported.");
        }

        JsonElement indexUriElement = rootElement.GetProperty("indexUri");
        if (indexUriElement.ValueKind != JsonValueKind.String)
        {
            throw new CatalogSourceConfigurationException("The catalog index address must be a string.");
        }

        string? indexUriTextOrNull = indexUriElement.GetString();
        if (string.IsNullOrWhiteSpace(indexUriTextOrNull))
        {
            throw new CatalogSourceConfigurationException("The catalog index address cannot be empty.");
        }

        return createConfiguration(indexUriTextOrNull, ECatalogSourceOrigin.LocalFile);
    }

    internal static CatalogSourceConfiguration createFromEnvironment(string value)
    {
        return createConfiguration(value, ECatalogSourceOrigin.Environment);
    }

    private static CatalogSourceConfiguration createConfiguration(string indexUriText, ECatalogSourceOrigin origin)
    {
        Uri? indexUriOrNull;
        bool isValidUri = Uri.TryCreate(indexUriText.Trim(), UriKind.Absolute, out indexUriOrNull);
        if (isValidUri == false || indexUriOrNull == null)
        {
            throw new CatalogSourceConfigurationException("The catalog index address must be an absolute URI.");
        }

        CatalogIndexEndpoint endpoint = new CatalogIndexEndpoint(indexUriOrNull);
        return new CatalogSourceConfiguration(endpoint, origin);
    }
}
