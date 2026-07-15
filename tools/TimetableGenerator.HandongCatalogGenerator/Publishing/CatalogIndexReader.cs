using System;
using System.Collections.Generic;
using System.Text.Json;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal static class CatalogIndexReader
{
    private const int CATALOG_SCHEMA_VERSION = 1;
    private const int INDEX_SCHEMA_VERSION = 1;

    public static CatalogIndexDocument Read(ReadOnlyMemory<byte> content)
    {
        try
        {
            using (JsonDocument jsonDocument = JsonDocument.Parse(content))
            {
                return readDocument(jsonDocument.RootElement);
            }
        }
        catch (CatalogIndexFormatException)
        {
            throw;
        }
        catch (Exception error) when (
            error is JsonException
            || error is InvalidOperationException
            || error is KeyNotFoundException
            || error is FormatException
            || error is ArgumentException)
        {
            throw new CatalogIndexFormatException("The existing catalog index is invalid.", error);
        }
    }

    private static CatalogIndexDocument readDocument(JsonElement root)
    {
        requireString(root, "documentType", "courseCatalogIndex");
        requireNumber(root, "schemaVersion", INDEX_SCHEMA_VERSION);
        string defaultCatalogId = getRequiredString(root, "defaultCatalogId");
        JsonElement entriesElement = root.GetProperty("catalogs");
        if (entriesElement.ValueKind != JsonValueKind.Array)
        {
            throw new CatalogIndexFormatException("The catalogs property must be an array.");
        }

        List<CatalogIndexEntry> entries = new List<CatalogIndexEntry>();
        CatalogIndexEntry? defaultEntryOrNull = null;
        foreach (JsonElement entryElement in entriesElement.EnumerateArray())
        {
            CatalogIndexEntry entry = readEntry(entryElement);
            entries.Add(entry);
            if (string.Equals(entry.CatalogId, defaultCatalogId, StringComparison.Ordinal))
            {
                defaultEntryOrNull = entry;
            }
        }

        if (defaultEntryOrNull == null)
        {
            throw new CatalogIndexFormatException("The default catalog ID is not present in the index.");
        }

        return new CatalogIndexDocument(defaultEntryOrNull, entries);
    }

    private static CatalogIndexEntry readEntry(JsonElement element)
    {
        requireNumber(element, "catalogSchemaVersion", CATALOG_SCHEMA_VERSION);
        JsonElement institution = element.GetProperty("institution");
        requireString(institution, "id", CatalogFileLayout.INSTITUTION_ID);
        JsonElement institutionName = institution.GetProperty("name");
        requireString(institutionName, "ko", CatalogFileLayout.INSTITUTION_NAME_KO);
        requireString(institutionName, "en", CatalogFileLayout.INSTITUTION_NAME_EN);

        JsonElement termElement = element.GetProperty("term");
        AcademicTerm term = new AcademicTerm(
            new AcademicYear(getRequiredInt32(termElement, "academicYear")),
            new AcademicSemester(getRequiredInt32(termElement, "semester")));
        requireString(termElement, "id", term.Id);

        CatalogRevision revision = new CatalogRevision(getRequiredInt32(element, "revision"));
        requireString(element, "catalogId", CatalogFileLayout.GetCatalogId(term, revision));

        JsonElement fileElement = element.GetProperty("file");
        requireString(fileElement, "relativePath", CatalogFileLayout.GetCatalogRelativePath(term, revision));
        requireString(fileElement, "mediaType", "application/json");
        requireString(fileElement, "charset", "utf-8");
        requireString(fileElement, "contentEncoding", "identity");
        CatalogFileSize fileSize = new CatalogFileSize(getRequiredInt64(fileElement, "sizeBytes"));
        Sha256Digest sha256 = Sha256Digest.Parse(getRequiredString(fileElement, "sha256"));

        JsonElement countsElement = element.GetProperty("counts");
        CatalogItemCount courseCount = new CatalogItemCount(getRequiredInt32(countsElement, "courses"));
        CatalogItemCount offeringCount = new CatalogItemCount(getRequiredInt32(countsElement, "offerings"));

        return new CatalogIndexEntry(
            term,
            revision,
            fileSize,
            sha256,
            courseCount,
            offeringCount);
    }

    private static int getRequiredInt32(JsonElement element, string propertyName)
    {
        int value;
        bool isNumber = element.GetProperty(propertyName).TryGetInt32(out value);
        if (isNumber == false)
        {
            throw new CatalogIndexFormatException(propertyName + " must be a 32-bit integer.");
        }

        return value;
    }

    private static long getRequiredInt64(JsonElement element, string propertyName)
    {
        long value;
        bool isNumber = element.GetProperty(propertyName).TryGetInt64(out value);
        if (isNumber == false)
        {
            throw new CatalogIndexFormatException(propertyName + " must be a 64-bit integer.");
        }

        return value;
    }

    private static string getRequiredString(JsonElement element, string propertyName)
    {
        string? valueOrNull = element.GetProperty(propertyName).GetString();
        if (valueOrNull == null)
        {
            throw new CatalogIndexFormatException(propertyName + " must be a string.");
        }

        return valueOrNull;
    }

    private static void requireNumber(JsonElement element, string propertyName, int expectedValue)
    {
        int actualValue = getRequiredInt32(element, propertyName);
        if (actualValue != expectedValue)
        {
            throw new CatalogIndexFormatException(propertyName + " uses an unsupported value.");
        }
    }

    private static void requireString(JsonElement element, string propertyName, string expectedValue)
    {
        string actualValue = getRequiredString(element, propertyName);
        if (string.Equals(actualValue, expectedValue, StringComparison.Ordinal) == false)
        {
            throw new CatalogIndexFormatException(propertyName + " uses an unsupported value.");
        }
    }
}
