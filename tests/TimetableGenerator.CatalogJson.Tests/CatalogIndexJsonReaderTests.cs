using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TimetableGenerator.CatalogJson.Tests;

[TestClass]
public sealed class CatalogIndexJsonReaderTests
{
    private static readonly Sha256Digest VALID_SHA256 = new Sha256Digest(
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");

    [TestMethod]
    public void ReadValidIndexCreatesStronglyTypedEntry()
    {
        CatalogFileSize fileSize = new CatalogFileSize(1_024L);
        byte[] indexBytes = CatalogJsonTestDocuments.CreateValidIndexBytes(
            fileSize,
            VALID_SHA256);

        CatalogIndexDocument document = CatalogIndexJsonReader.Read(indexBytes);
        CatalogIndexEntry defaultEntry = document.FindDefaultEntry();

        Assert.AreEqual("handong-global-university:2026-2:r0001", document.DefaultCatalogId.Value);
        Assert.AreEqual("한동대학교", defaultEntry.Institution.KoreanName.Value);
        Assert.AreEqual("Handong Global University", defaultEntry.Institution.EnglishName.Value);
        Assert.AreEqual(CatalogJsonTestDocuments.VALID_RELATIVE_PATH, defaultEntry.File.RelativePath.Value);
        Assert.AreEqual(fileSize, defaultEntry.File.Size);
        Assert.AreEqual(VALID_SHA256, defaultEntry.File.Sha256);
        Assert.AreEqual(1, defaultEntry.Counts.CourseCount.Value);
        Assert.AreEqual(2, defaultEntry.Counts.OfferingCount.Value);
    }

    [TestMethod]
    public void ReadRejectsDuplicateProperty()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidIndexBytes(
            new CatalogFileSize(1L),
            VALID_SHA256);
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"schemaVersion\": 1,");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CatalogIndexJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.schemaVersion", exception.JsonPath);
        StringAssert.Contains(exception.Message, "duplicate");
    }

    [TestMethod]
    public void ReadRejectsUnknownNestedProperty()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidIndexBytes(
            new CatalogFileSize(1L),
            VALID_SHA256);
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "\"counts\": {\n        \"courses\": 1,",
            "\"counts\": {\n        \"unexpected\": 0,\n        \"courses\": 1,");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CatalogIndexJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.catalogs[0].counts.unexpected", exception.JsonPath);
        StringAssert.Contains(exception.Message, "not defined");
    }

    [TestMethod]
    public void ReadRejectsMissingRequiredProperty()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidIndexBytes(
            new CatalogFileSize(1L),
            VALID_SHA256);
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "  \"defaultCatalogId\": \"handong-global-university:2026-2:r0001\",\n",
            string.Empty);

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CatalogIndexJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.defaultCatalogId", exception.JsonPath);
        StringAssert.Contains(exception.Message, "missing");
    }

    [TestMethod]
    public void ReadRejectsUnsupportedSchemaVersion()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidIndexBytes(
            new CatalogFileSize(1L),
            VALID_SHA256);
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "\"schemaVersion\": 1",
            "\"schemaVersion\": 2");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CatalogIndexJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.schemaVersion", exception.JsonPath);
        StringAssert.Contains(exception.Message, "schemaVersion 1");
    }

    [TestMethod]
    public void ReadRejectsUppercaseSha256()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidIndexBytes(
            new CatalogFileSize(1L),
            VALID_SHA256);
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            VALID_SHA256.HexValue,
            VALID_SHA256.HexValue.ToUpperInvariant());

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CatalogIndexJsonReader.Read(invalidBytes));

        StringAssert.Contains(exception.Message, "lowercase hexadecimal");
    }

    [TestMethod]
    [DataRow("/handong-global-university/2026-2/catalog-r0001.json")]
    [DataRow("C:/catalog/catalog-r0001.json")]
    [DataRow("https://example.com/catalog-r0001.json")]
    [DataRow("//example.com/catalog-r0001.json")]
    [DataRow("handong-global-university/../catalog-r0001.json")]
    [DataRow("handong-global-university/%2e%2e/catalog-r0001.json")]
    [DataRow("handong-global-university/2026-2/catalog-r0001.json?download=1")]
    [DataRow("handong-global-university/2026-2/catalog-r0001.json#latest")]
    [DataRow("handong-global-university\\2026-2\\catalog-r0001.json")]
    public void CatalogRelativePathRejectsUnsafeValue(string unsafePath)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CatalogRelativePath(unsafePath));
    }

    [TestMethod]
    public void ReadRejectsSafeButUnexpectedCatalogPath()
    {
        byte[] invalidBytes = CatalogJsonTestDocuments.CreateIndexBytes(
            "handong-global-university/2026-2/other.json",
            new CatalogFileSize(1L),
            VALID_SHA256);

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CatalogIndexJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.catalogs[0].file.relativePath", exception.JsonPath);
    }

    [TestMethod]
    public void CatalogFileDescriptorRejectsTheDefaultInvalidFileSize()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => new CatalogFileDescriptor(
                new CatalogRelativePath(CatalogJsonTestDocuments.VALID_RELATIVE_PATH),
                new CatalogMediaType("application/json"),
                new CatalogCharset("utf-8"),
                new CatalogContentEncoding("identity"),
                default(CatalogFileSize),
                VALID_SHA256));
    }
}
