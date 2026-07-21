using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TimetableGenerator.HandongCatalogGenerator.Application;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Tests.Handong.Source;
using TimetableGenerator.Infrastructure.Catalogs;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Application;

[TestClass]
public sealed class CatalogPublishingContractTests
{
    [TestMethod]
    public async Task GeneratedPackageIsAcceptedByTheApplicationRuntimeAsync()
    {
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(
            HandongExportTestHtml.Create()))
        using (TemporaryCatalogOutputRoot outputRoot = new TemporaryCatalogOutputRoot())
        {
            CatalogGenerationRequest request = new CatalogGenerationRequest(
                sourceFile.FilePath,
                AcademicTerm.Parse("2026-2"),
                new CatalogRevision(1),
                outputRoot.OutputRootPath);
            CatalogGenerationService service = new CatalogGenerationService();

            CatalogGenerationResult result = await service.GenerateAsync(
                request,
                CancellationToken.None);
            byte[] indexBytes = await File.ReadAllBytesAsync(result.IndexPath.Value);
            byte[] catalogBytes = await File.ReadAllBytesAsync(result.CatalogPath.Value);

            VerifiedCatalogPackage package = VerifiedCatalogPackage.ReadAndVerify(
                indexBytes,
                catalogBytes);

            Assert.AreEqual(package.Entry.CatalogId, package.Document.Catalog.Id);
            Assert.AreEqual(result.CatalogFileSize.Value, package.Entry.File.Size.Value);
            Assert.AreEqual(result.CatalogSha256.HexValue, package.Entry.File.Sha256.HexValue);
            Assert.AreEqual(
                result.Summary.CourseCount.Value,
                package.Document.Counts.CourseCount.Value);
            Assert.AreEqual(
                result.Summary.OfferingCount.Value,
                package.Document.Counts.OfferingCount.Value);
            Assert.AreEqual(
                result.Summary.ScheduledOfferingCount.Value,
                package.Document.Counts.ScheduledOfferingCount.Value);
            Assert.HasCount(
                result.Summary.CourseCount.Value,
                package.Document.Catalog.Courses);
            Assert.HasCount(
                result.Summary.OfferingCount.Value,
                package.Document.Catalog.Offerings);
            Assert.HasCount(2, package.Document.Catalog.Offerings[0].MeetingSchedule.Slots);
            Assert.AreEqual(
                "테스트 담당자",
                package.Document.OfferingMetadata[0]
                    .Instruction
                    .InstructorAssignment
                    .GetDisplayText()
                    .Value);
            Assert.AreEqual(
                "HDH 403",
                package.Document.OfferingMetadata[0]
                    .Logistics
                    .Location
                    .GetDisplayText()
                    .Value);
        }
    }
}
