using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson.Tests;

[TestClass]
public sealed class CourseCatalogJsonReaderTests
{
    [TestMethod]
    public void ReadValidCatalogConvertsDomainAndPreservesOfferingMetadata()
    {
        byte[] catalogBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();

        CourseCatalogDocument document = CourseCatalogJsonReader.Read(catalogBytes);
        CatalogOfferingMetadata scheduledMetadata = document.OfferingMetadata[0];
        CatalogOfferingMetadata unscheduledMetadata = document.OfferingMetadata[1];

        Assert.AreEqual("handong-global-university:2026-2:r0001", document.Catalog.Id.Value);
        Assert.HasCount(1, document.Catalog.Courses);
        Assert.HasCount(2, document.Catalog.Offerings);
        Assert.IsTrue(document.Catalog.Offerings[0].MeetingSchedule.IsScheduled);
        Assert.IsFalse(document.Catalog.Offerings[1].MeetingSchedule.IsScheduled);
        Assert.AreEqual("Handong Global University", document.Institution.EnglishName.Value);
        Assert.AreEqual("전산전자공학부", scheduledMetadata.Classification.OfferingUnitName.Value);
        Assert.AreEqual(ERequirementType.MajorRequired, scheduledMetadata.Classification.RequirementType);
        Assert.AreEqual("홍길동 외 1명", scheduledMetadata.Instruction.InstructorAssignment.GetDisplayText().Value);
        Assert.AreEqual(1, scheduledMetadata.Instruction.InstructorAssignment.GetAdditionalInstructorCount().Value);
        Assert.AreEqual("월1", scheduledMetadata.Logistics.GetScheduleSourceText().Value);
        Assert.AreEqual("오석관 301", scheduledMetadata.Logistics.Location.GetDisplayText().Value);
        Assert.AreEqual(20, scheduledMetadata.Capacity.GetCurrentEnrollment().Value);
        Assert.AreEqual(50m, scheduledMetadata.Instruction.EnglishInstructionPercentage.Value);
        Assert.AreEqual("전문교양", scheduledMetadata.Classification.GetGeneralEducationCategory().Value);
        Assert.AreEqual(EInstructorAssignmentStatus.Unconfirmed, unscheduledMetadata.Instruction.InstructorAssignment.Status);
        Assert.AreEqual(ELocationAssignmentStatus.NotProvided, unscheduledMetadata.Logistics.Location.Status);
        Assert.IsFalse(unscheduledMetadata.Logistics.HasScheduleSourceText);
        Assert.IsFalse(unscheduledMetadata.Capacity.HasCurrentEnrollment);
    }

    [TestMethod]
    public void ReadRejectsUnknownDeepProperty()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "\"additionalInstructorCount\": 1",
            "\"additionalInstructorCount\": 1,\n        \"unexpected\": true");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.offerings[0].instructorAssignment.unexpected", exception.JsonPath);
    }

    [TestMethod]
    public void ReadRejectsDuplicateDeepProperty()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "\"period\": 1",
            "\"period\": 1,\n            \"period\": 1");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.offerings[0].schedule.slots[0].period", exception.JsonPath);
        StringAssert.Contains(exception.Message, "duplicate");
    }

    [TestMethod]
    public void ReadRejectsUnsupportedCatalogSchemaVersion()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "\"schemaVersion\": 1",
            "\"schemaVersion\": 2");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.schemaVersion", exception.JsonPath);
    }

    [TestMethod]
    public void ReadRejectsDeclaredCourseCountMismatch()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "\"courses\": 1,",
            "\"courses\": 2,");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.counts.courses", exception.JsonPath);
        StringAssert.Contains(exception.Message, "does not match");
    }

    [TestMethod]
    public void ReadRejectsDataQualityStatusMismatch()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "\"roomNotProvided\": 1,",
            "\"roomNotProvided\": 0,");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.dataQuality.roomNotProvided", exception.JsonPath);
    }

    [TestMethod]
    public void ReadRejectsScheduleStatusAndValueMismatch()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(
            validBytes,
            "\"status\": \"notProvided\",\n        \"sourceTextKo\": null,",
            "\"status\": \"scheduled\",\n        \"sourceTextKo\": null,");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.offerings[1].schedule", exception.JsonPath);
        StringAssert.Contains(exception.Message, "require Korean source text");
    }

    [TestMethod]
    public void ReadWithIndexRejectsChangedCatalogBytes()
    {
        byte[] catalogBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        Sha256Digest catalogSha256 = Sha256Digest.Compute(catalogBytes);
        CatalogFileSize catalogSize = new CatalogFileSize(catalogBytes.Length);
        byte[] indexBytes = CatalogJsonTestDocuments.CreateValidIndexBytes(
            catalogSize,
            catalogSha256);
        CatalogIndexEntry indexEntry = CatalogIndexJsonReader.Read(indexBytes).FindDefaultEntry();
        byte[] changedCatalogBytes = CatalogJsonTestDocuments.Replace(
            catalogBytes,
            "Data Structures",
            "Changed Data Structures");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.ReadAndVerify(changedCatalogBytes, indexEntry));

        Assert.AreEqual("$", exception.JsonPath);
        StringAssert.Contains(exception.Message, "SHA-256");
    }

    [TestMethod]
    public void OfferingMetadataRejectsTheDefaultInvalidSourceRecordNumber()
    {
        CourseCatalogDocument document = CourseCatalogJsonReader.Read(
            CatalogJsonTestDocuments.CreateValidCatalogBytes());
        CatalogOfferingMetadata metadata = document.OfferingMetadata[0];

        Assert.ThrowsExactly<ArgumentException>(
            () => new CatalogOfferingMetadata(
                metadata.OfferingId,
                metadata.Classification,
                metadata.Instruction,
                metadata.Logistics,
                metadata.Capacity,
                metadata.Details,
                default(SourceRecordNumber)));
    }

    [TestMethod]
    public void ReadActualGeneratedArtifactWhenAvailable()
    {
        string? repositoryRootOrNull = findRepositoryRootOrNull();
        if (repositoryRootOrNull == null)
        {
            return;
        }

        string deploymentRoot = Path.Combine(
            repositoryRootOrNull,
            "deploy",
            "dothome",
            "html",
            "timetable-generator",
            "catalog",
            "v1");
        string indexPath = Path.Combine(deploymentRoot, "index.json");
        string catalogPath = Path.Combine(
            deploymentRoot,
            "handong-global-university",
            "2026-2",
            "catalog-r0001.json");
        if (File.Exists(indexPath) == false || File.Exists(catalogPath) == false)
        {
            return;
        }

        byte[] indexBytes = File.ReadAllBytes(indexPath);
        byte[] catalogBytes = File.ReadAllBytes(catalogPath);
        CatalogIndexEntry indexEntry = CatalogIndexJsonReader.Read(indexBytes).FindDefaultEntry();

        CourseCatalogDocument firstDocument = CourseCatalogJsonReader.ReadAndVerify(
            catalogBytes,
            indexEntry);
        CourseCatalogDocument secondDocument = CourseCatalogJsonReader.ReadAndVerify(
            catalogBytes,
            indexEntry);

        Assert.AreEqual(972_395L, indexEntry.File.Size.Value);
        Assert.AreEqual(
            "fb66ad08f8f884dfa625910ec78bcaae7d445709be97e13a37e4e7761329097b",
            indexEntry.File.Sha256.HexValue);
        Assert.HasCount(515, firstDocument.Catalog.Courses);
        Assert.HasCount(742, firstDocument.Catalog.Offerings);
        Assert.AreEqual(657, firstDocument.Counts.ScheduledOfferingCount.Value);
        Assert.AreEqual(85, firstDocument.Counts.MeetingNotProvidedCount.Value);
        Assert.AreEqual(93, firstDocument.DataQuality.InstructorUnconfirmedCount.Value);
        Assert.AreEqual(92, firstDocument.DataQuality.RoomNotProvidedCount.Value);
        Assert.HasCount(742, firstDocument.OfferingMetadata);
        Assert.AreEqual(firstDocument.Catalog.Id, secondDocument.Catalog.Id);
        Assert.AreEqual(
            firstDocument.OfferingMetadata[0].OfferingId,
            secondDocument.OfferingMetadata[0].OfferingId);
    }

    private static string? findRepositoryRootOrNull()
    {
        DirectoryInfo? currentDirectoryOrNull = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectoryOrNull != null)
        {
            string globalJsonPath = Path.Combine(currentDirectoryOrNull.FullName, "global.json");
            if (File.Exists(globalJsonPath))
            {
                return currentDirectoryOrNull.FullName;
            }

            currentDirectoryOrNull = currentDirectoryOrNull.Parent;
        }

        return null;
    }
}
