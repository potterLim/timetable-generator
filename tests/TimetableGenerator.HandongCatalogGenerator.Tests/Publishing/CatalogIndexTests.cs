using System;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Publishing;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Publishing;

[TestClass]
public sealed class CatalogIndexTests
{
    [TestMethod]
    public void WriteThenRead_ValidIndex_PreservesEntriesAndHashMetadata()
    {
        CatalogIndexEntry entry = createEntry("2026-2", 1, 'a');
        CatalogIndexDocument document = new CatalogIndexDocument(entry, new[] { entry });

        byte[] content = CatalogIndexJsonWriter.Write(document);
        CatalogIndexDocument parsed = CatalogIndexReader.Read(content);

        Assert.AreEqual(entry.CatalogId, parsed.DefaultCatalogId);
        Assert.HasCount(1, parsed.Entries);
        Assert.AreEqual(entry.Sha256, parsed.Entries[0].Sha256);
        Assert.AreEqual((byte)'\n', content[^1]);
        string contentText = Encoding.UTF8.GetString(content);
        Assert.IsTrue(contentText.Contains("handong-global-university/2026-2/catalog-r0001.json", StringComparison.Ordinal));
        Assert.IsFalse(contentText.Contains("updatedAt", StringComparison.Ordinal));
        Assert.IsFalse(contentText.Contains("publishedAt", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateWithUpsertedEntry_ExistingRevisions_PreservesAndSortsEntries()
    {
        CatalogIndexEntry revisionTwo = createEntry("2026-2", 2, 'b');
        CatalogIndexEntry previousTerm = createEntry("2026-1", 1, 'a');
        CatalogIndexEntry revisionOne = createEntry("2026-2", 1, 'c');

        CatalogIndexDocument document = CatalogIndexDocument.CreateWithUpsertedEntry(revisionOne, new[] { revisionTwo, previousTerm });

        Assert.HasCount(3, document.Entries);
        Assert.AreEqual(previousTerm.CatalogId, document.Entries[0].CatalogId);
        Assert.AreEqual(revisionOne.CatalogId, document.Entries[1].CatalogId);
        Assert.AreEqual(revisionTwo.CatalogId, document.Entries[2].CatalogId);
        Assert.AreEqual(revisionOne.CatalogId, document.DefaultCatalogId);
    }

    [TestMethod]
    public void Read_PreReleaseTimestampFields_AcceptsAndDropsUnusedMetadata()
    {
        CatalogIndexEntry entry = createEntry("2026-2", 1, 'a');
        CatalogIndexDocument document = new CatalogIndexDocument(entry, new[] { entry });
        string currentContent = Encoding.UTF8.GetString(CatalogIndexJsonWriter.Write(document));
        string preReleaseContent = currentContent.Replace("  \"schemaVersion\": 1,\n", "  \"schemaVersion\": 1,\n  \"updatedAt\": \"2026-07-16T00:00:00Z\",\n", StringComparison.Ordinal).Replace("      \"revision\": 1,\n", "      \"revision\": 1,\n      \"publishedAt\": \"2026-07-16T00:00:00Z\",\n", StringComparison.Ordinal);

        CatalogIndexDocument parsed = CatalogIndexReader.Read(Encoding.UTF8.GetBytes(preReleaseContent));
        string rewrittenContent = Encoding.UTF8.GetString(CatalogIndexJsonWriter.Write(parsed));

        Assert.HasCount(1, parsed.Entries);
        using (JsonDocument rewrittenDocument = JsonDocument.Parse(rewrittenContent))
        {
            Assert.AreEqual(1, rewrittenDocument.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.AreEqual(1, rewrittenDocument.RootElement.GetProperty("catalogs")[0].GetProperty("catalogSchemaVersion").GetInt32());
        }

        Assert.IsFalse(rewrittenContent.Contains("updatedAt", StringComparison.Ordinal));
        Assert.IsFalse(rewrittenContent.Contains("publishedAt", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Read_UnsupportedIndexSchemaVersion_RejectsDocument()
    {
        CatalogIndexEntry entry = createEntry("2026-2", 1, 'a');
        CatalogIndexDocument document = new CatalogIndexDocument(entry, new[] { entry });
        string unsupportedContent = Encoding.UTF8.GetString(CatalogIndexJsonWriter.Write(document)).Replace("  \"schemaVersion\": 1,\n", "  \"schemaVersion\": 2,\n", StringComparison.Ordinal);

        Assert.ThrowsExactly<CatalogIndexFormatException>(() => CatalogIndexReader.Read(Encoding.UTF8.GetBytes(unsupportedContent)));
    }

    private static CatalogIndexEntry createEntry(string term, int revision, char digestCharacter)
    {
        return new CatalogIndexEntry(
            AcademicTerm.Parse(term),
            new CatalogRevision(revision),
            new CatalogFileSize(1024),
            Sha256Digest.Parse(new string(digestCharacter, 64)),
            new CatalogItemCount(515),
            new CatalogItemCount(742));
    }
}
