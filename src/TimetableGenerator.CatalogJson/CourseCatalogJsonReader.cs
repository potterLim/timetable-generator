using System;
using System.Collections.Generic;
using System.Text.Json;
using TimetableGenerator.CatalogJson.Internal;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public static partial class CourseCatalogJsonReader
{
    public static CourseCatalogDocument Read(ReadOnlyMemory<byte> jsonBytes)
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
            throw new CatalogJsonFormatException(
                "$",
                "a schema value is invalid. " + exception.Message,
                exception);
        }
    }

    public static CourseCatalogDocument ReadAndVerify(
        ReadOnlyMemory<byte> jsonBytes,
        CatalogIndexEntry expectedEntry)
    {
        if (expectedEntry == null)
        {
            throw new ArgumentNullException(nameof(expectedEntry));
        }

        if (expectedEntry.File.HasExpectedContent(jsonBytes.Span) == false)
        {
            throw new CatalogJsonFormatException(
                "$",
                "the catalog bytes do not match the size and SHA-256 declared by the index.");
        }

        CourseCatalogDocument document = Read(jsonBytes);
        validateAgainstIndex(document, expectedEntry);
        return document;
    }

    private static CourseCatalogDocument parseDocument(JsonElement rootElement)
    {
        StrictJsonObject rootObject = StrictJsonObject.Create(
            rootElement,
            "$",
            new string[]
            {
                "documentType",
                "schemaVersion",
                "catalogId",
                "revision",
                "institution",
                "term",
                "source",
                "converter",
                "counts",
                "dataQuality",
                "courses",
                "offerings",
            });
        CatalogJsonValueParser.RequireExactString(
            rootObject.GetString("documentType"),
            CatalogJsonSchema.CATALOG_DOCUMENT_TYPE,
            rootObject.GetPropertyPath("documentType"));
        CatalogJsonValueParser.RequireSchemaVersion(
            rootObject.GetInt32("schemaVersion"),
            rootObject.GetPropertyPath("schemaVersion"));

        CatalogId catalogId = new CatalogId(rootObject.GetString("catalogId"));
        CatalogRevision revision = new CatalogRevision(rootObject.GetInt32("revision"));
        InstitutionMetadata institution = CatalogJsonValueParser.ParseInstitution(
            rootObject.GetElement("institution"),
            rootObject.GetPropertyPath("institution"));
        AcademicTerm term = CatalogJsonValueParser.ParseTerm(
            rootObject.GetElement("term"),
            rootObject.GetPropertyPath("term"));
        string expectedCatalogId = CatalogJsonValueParser.BuildCatalogId(institution.Id, term, revision);
        CatalogJsonValueParser.RequireExactString(
            catalogId.Value,
            expectedCatalogId,
            rootObject.GetPropertyPath("catalogId"));

        CatalogSourceMetadata source = parseSource(
            rootObject.GetElement("source"),
            rootObject.GetPropertyPath("source"),
            institution.Id,
            term);
        CatalogConverterMetadata converter = parseConverter(
            rootObject.GetElement("converter"),
            rootObject.GetPropertyPath("converter"));
        CatalogDocumentCounts counts = parseDocumentCounts(
            rootObject.GetElement("counts"),
            rootObject.GetPropertyPath("counts"));

        List<CatalogCourse> courses = parseCourses(rootObject.GetArray("courses"), institution.Id);
        Dictionary<CourseId, CourseCode> courseCodesById = buildCourseCodesById(courses);
        List<CatalogOffering> offerings = new List<CatalogOffering>();
        List<CatalogOfferingMetadata> offeringMetadata = new List<CatalogOfferingMetadata>();
        parseOfferings(
            rootObject.GetArray("offerings"),
            institution.Id,
            term,
            courseCodesById,
            offerings,
            offeringMetadata);

        CatalogDataQualityMetadata dataQuality = parseDataQuality(
            rootObject.GetElement("dataQuality"),
            rootObject.GetPropertyPath("dataQuality"),
            courseCodesById.Keys);
        validateDocumentConsistency(counts, dataQuality, courses, offerings, offeringMetadata);

        CourseCatalog catalog = new CourseCatalog(
            catalogId,
            institution.Id,
            institution.KoreanName,
            term,
            revision,
            courses,
            offerings);
        return new CourseCatalogDocument(
            catalog,
            institution,
            source,
            converter,
            counts,
            dataQuality,
            offeringMetadata);
    }

    private static void validateAgainstIndex(
        CourseCatalogDocument document,
        CatalogIndexEntry expectedEntry)
    {
        CourseCatalog catalog = document.Catalog;
        if (catalog.Id != expectedEntry.CatalogId
            || catalog.InstitutionId != expectedEntry.Institution.Id
            || catalog.Term != expectedEntry.Term
            || catalog.Revision != expectedEntry.Revision
            || document.Institution.KoreanName != expectedEntry.Institution.KoreanName
            || document.Institution.EnglishName != expectedEntry.Institution.EnglishName)
        {
            throw new CatalogJsonFormatException("$", "the catalog identity does not match its index entry.");
        }

        if (document.Counts.CourseCount != expectedEntry.Counts.CourseCount
            || document.Counts.OfferingCount != expectedEntry.Counts.OfferingCount)
        {
            throw new CatalogJsonFormatException("$.counts", "catalog counts do not match the index entry.");
        }
    }
}
