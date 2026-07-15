using System.Text.Json;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal static class CatalogIndexJsonWriter
{
    private const int SCHEMA_VERSION = 1;

    public static byte[] Write(CatalogIndexDocument document)
    {
        return DeterministicJsonWriter.Write(writer => writeDocument(writer, document));
    }

    private static void writeDocument(Utf8JsonWriter writer, CatalogIndexDocument document)
    {
        writer.WriteStartObject();
        writer.WriteString("documentType", "courseCatalogIndex");
        writer.WriteNumber("schemaVersion", SCHEMA_VERSION);
        writer.WriteString("updatedAt", document.UpdatedAt.ToString());
        writer.WriteString("defaultCatalogId", document.DefaultCatalogId);
        writer.WriteStartArray("catalogs");
        foreach (CatalogIndexEntry entry in document.Entries)
        {
            writeEntry(writer, entry);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void writeEntry(Utf8JsonWriter writer, CatalogIndexEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("catalogId", entry.CatalogId);
        writer.WriteNumber("catalogSchemaVersion", SCHEMA_VERSION);
        writer.WriteStartObject("institution");
        writer.WriteString("id", CatalogFileLayout.INSTITUTION_ID);
        writer.WriteStartObject("name");
        writer.WriteString("ko", CatalogFileLayout.INSTITUTION_NAME_KO);
        writer.WriteString("en", CatalogFileLayout.INSTITUTION_NAME_EN);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteStartObject("term");
        writer.WriteString("id", entry.Term.Id);
        writer.WriteNumber("academicYear", entry.Term.AcademicYear.Value);
        writer.WriteNumber("semester", entry.Term.Semester.Value);
        writer.WriteEndObject();
        writer.WriteNumber("revision", entry.Revision.Value);
        writer.WriteString("publishedAt", entry.PublishedAt.ToString());
        writer.WriteStartObject("file");
        writer.WriteString("relativePath", entry.RelativePath);
        writer.WriteString("mediaType", "application/json");
        writer.WriteString("charset", "utf-8");
        writer.WriteString("contentEncoding", "identity");
        writer.WriteNumber("sizeBytes", entry.FileSize.Value);
        writer.WriteString("sha256", entry.Sha256.HexValue);
        writer.WriteEndObject();
        writer.WriteStartObject("counts");
        writer.WriteNumber("courses", entry.CourseCount.Value);
        writer.WriteNumber("offerings", entry.OfferingCount.Value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
