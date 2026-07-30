using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(validBytes, "\"additionalInstructorCount\": 1", "\"additionalInstructorCount\": 1,\n        \"unexpected\": true");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.offerings[0].instructorAssignment.unexpected", exception.JsonPath);
    }

    [TestMethod]
    public void ReadRejectsDuplicateDeepProperty()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(validBytes, "\"period\": 1", "\"period\": 1,\n            \"period\": 1");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.offerings[0].schedule.slots[0].period", exception.JsonPath);
        StringAssert.Contains(exception.Message, "duplicate");
    }

    [TestMethod]
    public void ReadRejectsUnsupportedCatalogSchemaVersion()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(validBytes, "\"schemaVersion\": 1", "\"schemaVersion\": 2");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.schemaVersion", exception.JsonPath);
    }

    [TestMethod]
    public void ReadRejectsDeclaredCourseCountMismatch()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(validBytes, "\"courses\": 1,", "\"courses\": 2,");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.counts.courses", exception.JsonPath);
        StringAssert.Contains(exception.Message, "does not match");
    }

    [TestMethod]
    public void ReadRejectsDataQualityStatusMismatch()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(validBytes, "\"roomNotProvided\": 1,", "\"roomNotProvided\": 0,");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.Read(invalidBytes));

        Assert.AreEqual("$.dataQuality.roomNotProvided", exception.JsonPath);
    }

    [TestMethod]
    public void ReadRejectsScheduleStatusAndValueMismatch()
    {
        byte[] validBytes = CatalogJsonTestDocuments.CreateValidCatalogBytes();
        byte[] invalidBytes = CatalogJsonTestDocuments.Replace(validBytes, "\"status\": \"notProvided\",\n        \"sourceTextKo\": null,", "\"status\": \"scheduled\",\n        \"sourceTextKo\": null,");

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
        byte[] indexBytes = CatalogJsonTestDocuments.CreateValidIndexBytes(catalogSize, catalogSha256);
        CatalogIndexEntry indexEntry = CatalogIndexJsonReader.Read(indexBytes).FindDefaultEntry();
        byte[] changedCatalogBytes = CatalogJsonTestDocuments.Replace(catalogBytes, "Data Structures", "Changed Data Structures");

        CatalogJsonFormatException exception = Assert.ThrowsExactly<CatalogJsonFormatException>(
            () => CourseCatalogJsonReader.ReadAndVerify(changedCatalogBytes, indexEntry));

        Assert.AreEqual("$", exception.JsonPath);
        StringAssert.Contains(exception.Message, "SHA-256");
    }

    [TestMethod]
    public void OfferingMetadataRejectsTheDefaultInvalidSourceRecordNumber()
    {
        CourseCatalogDocument document = CourseCatalogJsonReader.Read(CatalogJsonTestDocuments.CreateValidCatalogBytes());
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

}
