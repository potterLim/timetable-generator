using System;
using System.Text;
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
        CatalogPublicationTime publicationTime = CatalogPublicationTime.Parse("2026-07-16T00:00:00Z");
        CatalogIndexEntry entry = createEntry("2026-2", 1, publicationTime, 'a');
        CatalogIndexDocument document = new CatalogIndexDocument(
            publicationTime,
            entry,
            new[] { entry });

        byte[] content = CatalogIndexJsonWriter.Write(document);
        CatalogIndexDocument parsed = CatalogIndexReader.Read(content);

        Assert.AreEqual(entry.CatalogId, parsed.DefaultCatalogId);
        Assert.HasCount(1, parsed.Entries);
        Assert.AreEqual(entry.Sha256, parsed.Entries[0].Sha256);
        Assert.AreEqual((byte)'\n', content[^1]);
        Assert.IsTrue(
            Encoding.UTF8.GetString(content).Contains(
                "handong-global-university/2026-2/catalog-r0001.json",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateWithUpsertedEntry_ExistingRevisions_PreservesAndSortsEntries()
    {
        CatalogPublicationTime publicationTime = CatalogPublicationTime.Parse("2026-07-16T00:00:00Z");
        CatalogIndexEntry revisionTwo = createEntry("2026-2", 2, publicationTime, 'b');
        CatalogIndexEntry previousTerm = createEntry("2026-1", 1, publicationTime, 'a');
        CatalogIndexEntry revisionOne = createEntry("2026-2", 1, publicationTime, 'c');

        CatalogIndexDocument document = CatalogIndexDocument.CreateWithUpsertedEntry(
            publicationTime,
            revisionOne,
            new[] { revisionTwo, previousTerm });

        Assert.HasCount(3, document.Entries);
        Assert.AreEqual(previousTerm.CatalogId, document.Entries[0].CatalogId);
        Assert.AreEqual(revisionOne.CatalogId, document.Entries[1].CatalogId);
        Assert.AreEqual(revisionTwo.CatalogId, document.Entries[2].CatalogId);
        Assert.AreEqual(revisionOne.CatalogId, document.DefaultCatalogId);
    }

    private static CatalogIndexEntry createEntry(
        string term,
        int revision,
        CatalogPublicationTime publicationTime,
        char digestCharacter)
    {
        return new CatalogIndexEntry(
            AcademicTerm.Parse(term),
            new CatalogRevision(revision),
            publicationTime,
            new CatalogFileSize(1024),
            Sha256Digest.Parse(new string(digestCharacter, 64)),
            new CatalogItemCount(515),
            new CatalogItemCount(742));
    }
}
