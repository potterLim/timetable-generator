using System;
using System.Collections.Generic;
using System.Text.Json;
using TimetableGenerator.CatalogJson.Internal;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public static class CatalogIndexJsonReader
{
    public static CatalogIndexDocument Read(ReadOnlyMemory<byte> jsonBytes)
    {
        if (jsonBytes.IsEmpty)
        {
            throw new CatalogJsonFormatException("$", "the document cannot be empty.");
        }

        try
        {
            JsonDocumentOptions options = new JsonDocumentOptions();
            options.AllowTrailingCommas = false;
            options.CommentHandling = JsonCommentHandling.Disallow;
            options.MaxDepth = 64;
            using (JsonDocument document = JsonDocument.Parse(jsonBytes, options))
            {
                return parseDocument(document.RootElement);
            }
        }
        catch (CatalogJsonFormatException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new CatalogJsonFormatException("$", "the input is not valid UTF-8 JSON.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new CatalogJsonFormatException("$", "a schema value is invalid. " + exception.Message, exception);
        }
    }

    private static CatalogIndexDocument parseDocument(JsonElement rootElement)
    {
        StrictJsonObject rootObject = StrictJsonObject.Create(
            rootElement,
            "$",
            new string[]
            {
                "documentType",
                "schemaVersion",
                "defaultCatalogId",
                "catalogs",
            });
        CatalogJsonValueParser.RequireExactString(rootObject.GetString("documentType"), CatalogJsonSchema.INDEX_DOCUMENT_TYPE, rootObject.GetPropertyPath("documentType"));
        CatalogJsonValueParser.RequireSchemaVersion(rootObject.GetInt32("schemaVersion"), rootObject.GetPropertyPath("schemaVersion"));

        CatalogId defaultCatalogId = new CatalogId(rootObject.GetString("defaultCatalogId"));
        JsonElement catalogsElement = rootObject.GetArray("catalogs");
        List<CatalogIndexEntry> entries = new List<CatalogIndexEntry>();
        int entryIndex = 0;
        foreach (JsonElement entryElement in catalogsElement.EnumerateArray())
        {
            string entryPath = "$.catalogs[" + entryIndex + "]";
            entries.Add(parseEntry(entryElement, entryPath));
            ++entryIndex;
        }

        if (entries.Count == 0)
        {
            throw new CatalogJsonFormatException("$.catalogs", "at least one catalog entry is required.");
        }

        return new CatalogIndexDocument(defaultCatalogId, entries);
    }

    private static CatalogIndexEntry parseEntry(JsonElement element, string path)
    {
        StrictJsonObject entryObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "catalogId",
                "catalogSchemaVersion",
                "institution",
                "term",
                "revision",
                "file",
                "counts",
            });
        CatalogId catalogId = new CatalogId(entryObject.GetString("catalogId"));
        CatalogJsonValueParser.RequireSchemaVersion(entryObject.GetInt32("catalogSchemaVersion"), entryObject.GetPropertyPath("catalogSchemaVersion"));
        InstitutionMetadata institution = CatalogJsonValueParser.ParseInstitution(entryObject.GetElement("institution"), entryObject.GetPropertyPath("institution"));
        AcademicTerm term = CatalogJsonValueParser.ParseTerm(entryObject.GetElement("term"), entryObject.GetPropertyPath("term"));
        CatalogRevision revision = new CatalogRevision(entryObject.GetInt32("revision"));
        CatalogFileDescriptor file = parseFile(entryObject.GetElement("file"), entryObject.GetPropertyPath("file"));
        CatalogIndexCounts counts = parseCounts(entryObject.GetElement("counts"), entryObject.GetPropertyPath("counts"));

        string expectedCatalogId = CatalogJsonValueParser.BuildCatalogId(institution.Id, term, revision);
        CatalogJsonValueParser.RequireExactString(catalogId.Value, expectedCatalogId, entryObject.GetPropertyPath("catalogId"));
        string expectedRelativePath = CatalogJsonValueParser.BuildCatalogRelativePath(institution.Id, term, revision);
        CatalogJsonValueParser.RequireExactString(file.RelativePath.Value, expectedRelativePath, entryObject.GetPropertyPath("file") + ".relativePath");

        return new CatalogIndexEntry(catalogId, institution, term, revision, file, counts);
    }

    private static CatalogFileDescriptor parseFile(JsonElement element, string path)
    {
        StrictJsonObject fileObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "relativePath",
                "mediaType",
                "charset",
                "contentEncoding",
                "sizeBytes",
                "sha256",
            });
        CatalogRelativePath relativePath = new CatalogRelativePath(fileObject.GetString("relativePath"));
        string mediaTypeText = fileObject.GetString("mediaType");
        string charsetText = fileObject.GetString("charset");
        string contentEncodingText = fileObject.GetString("contentEncoding");
        CatalogJsonValueParser.RequireExactString(mediaTypeText, CatalogJsonSchema.JSON_MEDIA_TYPE, fileObject.GetPropertyPath("mediaType"));
        CatalogJsonValueParser.RequireExactString(charsetText, CatalogJsonSchema.UTF8_CHARSET, fileObject.GetPropertyPath("charset"));
        CatalogJsonValueParser.RequireExactString(contentEncodingText, CatalogJsonSchema.IDENTITY_CONTENT_ENCODING, fileObject.GetPropertyPath("contentEncoding"));
        CatalogMediaType mediaType = new CatalogMediaType(mediaTypeText);
        CatalogCharset charset = new CatalogCharset(charsetText);
        CatalogContentEncoding contentEncoding = new CatalogContentEncoding(contentEncodingText);
        CatalogFileSize fileSize = new CatalogFileSize(fileObject.GetInt64("sizeBytes"));
        Sha256Digest sha256 = new Sha256Digest(fileObject.GetString("sha256"));
        return new CatalogFileDescriptor(
            relativePath,
            mediaType,
            charset,
            contentEncoding,
            fileSize,
            sha256);
    }

    private static CatalogIndexCounts parseCounts(JsonElement element, string path)
    {
        StrictJsonObject countsObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "courses",
                "offerings",
            });
        return new CatalogIndexCounts(new CatalogCourseCount(countsObject.GetInt32("courses")), new CatalogOfferingCount(countsObject.GetInt32("offerings")));
    }
}
