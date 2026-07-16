using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TimetableGenerator.CatalogJson.Internal;

internal sealed class StrictJsonObject
{
    private readonly Dictionary<string, JsonElement> mPropertiesByName;

    public string Path { get; }

    private StrictJsonObject(
        string path,
        Dictionary<string, JsonElement> propertiesByName)
    {
        Path = path;
        mPropertiesByName = propertiesByName;
    }

    public static StrictJsonObject Create(
        JsonElement element,
        string path,
        IReadOnlyCollection<string> expectedPropertyNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new CatalogJsonFormatException(path, "an object is required.");
        }

        HashSet<string> expectedNames = new HashSet<string>(
            expectedPropertyNames,
            StringComparer.Ordinal);
        Dictionary<string, JsonElement> propertiesByName =
            new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (propertiesByName.TryAdd(property.Name, property.Value) == false)
            {
                throw new CatalogJsonFormatException(
                    path + "." + property.Name,
                    "duplicate properties are not allowed.");
            }

            if (expectedNames.Contains(property.Name) == false)
            {
                throw new CatalogJsonFormatException(
                    path + "." + property.Name,
                    "the property is not defined by catalog schema v1.");
            }
        }

        foreach (string expectedName in expectedNames)
        {
            if (propertiesByName.ContainsKey(expectedName) == false)
            {
                throw new CatalogJsonFormatException(
                    path + "." + expectedName,
                    "the required property is missing.");
            }
        }

        return new StrictJsonObject(path, propertiesByName);
    }

    public JsonElement GetElement(string propertyName)
    {
        JsonElement element;
        bool hasElement = mPropertiesByName.TryGetValue(propertyName, out element);
        if (hasElement == false)
        {
            throw new InvalidOperationException(
                "The strict JSON object was created without a required property.");
        }

        return element;
    }

    public JsonElement GetArray(string propertyName)
    {
        JsonElement element = GetElement(propertyName);
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "an array is required.");
        }

        return element;
    }

    public string GetString(string propertyName)
    {
        JsonElement element = GetElement(propertyName);
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "a string is required.");
        }

        string? valueOrNull = element.GetString();
        if (valueOrNull == null)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "a non-null string is required.");
        }

        return valueOrNull;
    }

    public string? GetNullableStringOrNull(string propertyName)
    {
        JsonElement element = GetElement(propertyName);
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return GetString(propertyName);
    }

    public int GetInt32(string propertyName)
    {
        JsonElement element = GetElement(propertyName);
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "a 32-bit integer is required.");
        }

        int value;
        bool isInteger = element.TryGetInt32(out value);
        if (isInteger == false)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "a 32-bit integer is required.");
        }

        return value;
    }

    public int? GetNullableInt32OrNull(string propertyName)
    {
        JsonElement element = GetElement(propertyName);
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return GetInt32(propertyName);
    }

    public long GetInt64(string propertyName)
    {
        JsonElement element = GetElement(propertyName);
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "a 64-bit integer is required.");
        }

        long value;
        bool isInteger = element.TryGetInt64(out value);
        if (isInteger == false)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "a 64-bit integer is required.");
        }

        return value;
    }

    public decimal GetDecimal(string propertyName)
    {
        JsonElement element = GetElement(propertyName);
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "a decimal number is required.");
        }

        decimal value;
        bool isDecimal = element.TryGetDecimal(out value);
        if (isDecimal == false)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "a decimal number is required.");
        }

        return value;
    }

    public bool GetBoolean(string propertyName)
    {
        JsonElement element = GetElement(propertyName);
        if (element.ValueKind != JsonValueKind.True
            && element.ValueKind != JsonValueKind.False)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "a Boolean value is required.");
        }

        return element.GetBoolean();
    }

    public void RequireNull(string propertyName)
    {
        JsonElement element = GetElement(propertyName);
        if (element.ValueKind != JsonValueKind.Null)
        {
            throw new CatalogJsonFormatException(
                GetPropertyPath(propertyName),
                "null is required by catalog schema v1.");
        }
    }

    public string GetPropertyPath(string propertyName)
    {
        return Path + "." + propertyName;
    }
}
