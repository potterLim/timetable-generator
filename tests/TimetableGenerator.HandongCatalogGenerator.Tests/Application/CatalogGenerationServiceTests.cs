using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.HandongCatalogGenerator.Application;
using TimetableGenerator.HandongCatalogGenerator.Application.Errors;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Tests.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Tests.Application;

[TestClass]
public sealed class CatalogGenerationServiceTests
{
    [TestMethod]
    public async Task GenerateAsync_ValidSourceTwice_ProducesIdenticalPackageBytesAsync()
    {
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(
            HandongExportTestHtml.Create()))
        using (TemporaryCatalogOutputRoot outputRoot = new TemporaryCatalogOutputRoot())
        {
            CatalogGenerationRequest request = createRequest(sourceFile, outputRoot, 1);
            CatalogGenerationService service = new CatalogGenerationService();

            CatalogGenerationResult firstResult = await service.GenerateAsync(
                request,
                CancellationToken.None);
            byte[] firstCatalog = await File.ReadAllBytesAsync(firstResult.CatalogPath.Value);
            byte[] firstIndex = await File.ReadAllBytesAsync(firstResult.IndexPath.Value);
            CatalogGenerationResult secondResult = await service.GenerateAsync(
                request,
                CancellationToken.None);
            byte[] secondCatalog = await File.ReadAllBytesAsync(secondResult.CatalogPath.Value);
            byte[] secondIndex = await File.ReadAllBytesAsync(secondResult.IndexPath.Value);

            CollectionAssert.AreEqual(firstCatalog, secondCatalog);
            CollectionAssert.AreEqual(firstIndex, secondIndex);
            Assert.AreEqual(firstResult.CatalogSha256, secondResult.CatalogSha256);
            Assert.AreEqual(1, secondResult.Summary.CourseCount.Value);
            Assert.AreEqual(1, secondResult.Summary.OfferingCount.Value);
            Assert.AreEqual(1, secondResult.Summary.ScheduledOfferingCount.Value);
            Assert.AreEqual(0, secondResult.Summary.MeetingNotProvidedCount.Value);
            assertCatalogDocument(firstCatalog);
        }
    }

    [TestMethod]
    public async Task GenerateAsync_UnconfirmedInstructorAndMissingMeeting_PreservesStatesAsync()
    {
        string sourceHtml = HandongExportTestHtml.Create()
            .Replace(
                "<td>GLS&nbsp;주간<br><font color=\"blue\">이상훈</font></td>",
                "<td>GLS&nbsp;주간<br><font color=\"blue\">Unconfirmed</font></td>",
                StringComparison.Ordinal)
            .Replace(
                "<td>화5,금5<br>Tue5,Fri5<br><br><br></td>",
                "<td>&nbsp;</td>",
                StringComparison.Ordinal);
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(sourceHtml))
        using (TemporaryCatalogOutputRoot outputRoot = new TemporaryCatalogOutputRoot())
        {
            CatalogGenerationService service = new CatalogGenerationService();

            CatalogGenerationResult result = await service.GenerateAsync(
                createRequest(sourceFile, outputRoot, 1),
                CancellationToken.None);
            using (JsonDocument document = JsonDocument.Parse(
                await File.ReadAllBytesAsync(result.CatalogPath.Value)))
            {
                JsonElement offering = document.RootElement.GetProperty("offerings")[0];
                JsonElement instructor = offering.GetProperty("instructorAssignment");
                JsonElement schedule = offering.GetProperty("schedule");

                Assert.AreEqual("unconfirmed", instructor.GetProperty("status").GetString());
                Assert.AreEqual(JsonValueKind.Null, instructor.GetProperty("displayText").ValueKind);
                Assert.AreEqual("notProvided", schedule.GetProperty("status").GetString());
                Assert.HasCount(0, schedule.GetProperty("slots").EnumerateArray());
            }

            Assert.AreEqual(1, result.Summary.InstructorUnconfirmedCount.Value);
            Assert.AreEqual(1, result.Summary.MeetingNotProvidedCount.Value);
        }
    }

    [TestMethod]
    public async Task GenerateAsync_DamagedEnglishSchedule_UsesKoreanScheduleAndReportsMismatchAsync()
    {
        string sourceHtml = HandongExportTestHtml.Create()
            .Replace("Tue5,Fri5", "Tue5,Fr", StringComparison.Ordinal)
            .Replace("이상훈</font>", "이상훈 외 2명</font>", StringComparison.Ordinal)
            .Replace("<td>2</td>", "<td>.5</td>", StringComparison.Ordinal);
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(sourceHtml))
        using (TemporaryCatalogOutputRoot outputRoot = new TemporaryCatalogOutputRoot())
        {
            CatalogGenerationService service = new CatalogGenerationService();

            CatalogGenerationResult result = await service.GenerateAsync(
                createRequest(sourceFile, outputRoot, 1),
                CancellationToken.None);
            using (JsonDocument document = JsonDocument.Parse(
                await File.ReadAllBytesAsync(result.CatalogPath.Value)))
            {
                JsonElement root = document.RootElement;
                JsonElement course = root.GetProperty("courses")[0];
                JsonElement offering = root.GetProperty("offerings")[0];
                JsonElement schedule = offering.GetProperty("schedule");
                JsonElement instructor = offering.GetProperty("instructorAssignment");

                Assert.AreEqual(0.5m, course.GetProperty("credits").GetDecimal());
                Assert.AreEqual("화5,금5", schedule.GetProperty("sourceTextKo").GetString());
                Assert.HasCount(2, schedule.GetProperty("slots").EnumerateArray());
                Assert.AreEqual("이상훈 외 2명", instructor.GetProperty("displayText").GetString());
                Assert.AreEqual(2, instructor.GetProperty("additionalInstructorCount").GetInt32());
            }

            Assert.AreEqual(1, result.Summary.EnglishScheduleMismatchCount.Value);
        }
    }

    [TestMethod]
    public async Task GenerateAsync_ExistingRevisionHasDifferentBytes_RejectsOverwriteAsync()
    {
        using (TemporaryHandongSourceFile sourceFile = new TemporaryHandongSourceFile(
            HandongExportTestHtml.Create()))
        using (TemporaryCatalogOutputRoot outputRoot = new TemporaryCatalogOutputRoot())
        {
            CatalogGenerationRequest request = createRequest(sourceFile, outputRoot, 1);
            CatalogGenerationService service = new CatalogGenerationService();
            CatalogGenerationResult initialResult = await service.GenerateAsync(
                request,
                CancellationToken.None);
            await File.WriteAllTextAsync(initialResult.CatalogPath.Value, "different content");

            CatalogGenerationException exception =
                await Assert.ThrowsExactlyAsync<CatalogGenerationException>(
                    () => service.GenerateAsync(request, CancellationToken.None));

            Assert.AreEqual(ECatalogGenerationErrorCode.OutputConflict, exception.ErrorCode);
            Assert.AreEqual(ECatalogGeneratorExitCode.OutputFailure, exception.ExitCode);
            Assert.AreEqual(
                "different content",
                await File.ReadAllTextAsync(initialResult.CatalogPath.Value));
        }
    }

    private static CatalogGenerationRequest createRequest(
        TemporaryHandongSourceFile sourceFile,
        TemporaryCatalogOutputRoot outputRoot,
        int revision)
    {
        return new CatalogGenerationRequest(
            sourceFile.FilePath,
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(revision),
            outputRoot.OutputRootPath);
    }

    private static void assertCatalogDocument(byte[] content)
    {
        using (JsonDocument document = JsonDocument.Parse(content))
        {
            JsonElement root = document.RootElement;
            Assert.AreEqual("courseCatalog", root.GetProperty("documentType").GetString());
            Assert.AreEqual(1, root.GetProperty("counts").GetProperty("courses").GetInt32());
            JsonElement offering = root.GetProperty("offerings")[0];
            Assert.AreEqual("01", offering.GetProperty("sectionCode").GetString());
            Assert.AreEqual("scheduled", offering.GetProperty("schedule").GetProperty("status").GetString());
            Assert.AreEqual(
                "이상훈",
                offering.GetProperty("instructorAssignment").GetProperty("displayText").GetString());
        }
    }
}
