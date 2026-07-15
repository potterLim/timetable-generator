using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Core.Domain;
using TimetableGenerator.Infrastructure.Exporting;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class SchedulePngExporterTests
{
    [TestMethod]
    public async Task ExportCurrentAsyncCreatesMissingFoldersAndNeverOverwritesExistingPngsAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            ScheduleGridViewModel scheduleGrid = SchedulePngTestData.createScheduleGrid(
                "데이터베이스",
                EDay.Monday,
                1);
            SchedulePngExportRequest request = new SchedulePngExportRequest(
                scheduleGrid,
                new ScheduleExportNumber(3),
                new ScheduleExportDirectoryPath(testDirectoryPath),
                new ScheduleExportBaseName("2026 봄학기"));
            SchedulePngExporter exporter = new SchedulePngExporter();

            SchedulePngExportResult firstResult = await exporter.ExportCurrentAsync(
                request,
                CancellationToken.None);
            SchedulePngExportResult secondResult = await exporter.ExportCurrentAsync(
                request,
                CancellationToken.None);

            Assert.AreEqual(ESchedulePngExportCompletion.Succeeded, firstResult.Completion);
            Assert.AreEqual(ESchedulePngExportCompletion.Succeeded, secondResult.Completion);
            Assert.HasCount(1, firstResult.ExportedFiles);
            Assert.HasCount(1, secondResult.ExportedFiles);
            Assert.AreEqual(
                "2026 봄학기_시간표_03.png",
                firstResult.ExportedFiles[0].OutputFilePath.FileName);
            Assert.AreEqual(
                "2026 봄학기_시간표_03 (2).png",
                secondResult.ExportedFiles[0].OutputFilePath.FileName);
            Assert.IsTrue(File.Exists(firstResult.ExportedFiles[0].OutputFilePath.Value));
            Assert.IsTrue(File.Exists(secondResult.ExportedFiles[0].OutputFilePath.Value));
            CollectionAssert.AreEqual(
                File.ReadAllBytes(firstResult.ExportedFiles[0].OutputFilePath.Value),
                File.ReadAllBytes(secondResult.ExportedFiles[0].OutputFilePath.Value));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task ExportAllAsyncReportsStronglyTypedProgressForEveryScheduleAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            IReadOnlyList<ScheduleGridViewModel> scheduleGrids =
                SchedulePngTestData.createScheduleGrids();
            SchedulePngBatchExportRequest request = new SchedulePngBatchExportRequest(
                scheduleGrids,
                new ScheduleExportDirectoryPath(testDirectoryPath),
                new ScheduleExportBaseName("수강 계획"));
            RecordingExportProgress recordingProgress = new RecordingExportProgress();
            SchedulePngExporter exporter = new SchedulePngExporter();

            SchedulePngExportResult result = await exporter.ExportAllAsync(
                request,
                recordingProgress,
                CancellationToken.None);

            Assert.AreEqual(ESchedulePngExportCompletion.Succeeded, result.Completion);
            Assert.HasCount(2, result.ExportedFiles);
            Assert.IsEmpty(result.Failures);
            Assert.HasCount(2, recordingProgress.Values);
            Assert.AreEqual(1, recordingProgress.Values[0].ProcessedScheduleCount);
            Assert.AreEqual(2, recordingProgress.Values[0].TotalScheduleCount);
            Assert.AreEqual(1, recordingProgress.Values[0].ScheduleNumber.Value);
            Assert.AreEqual(
                ESchedulePngExportItemStatus.Succeeded,
                recordingProgress.Values[0].ItemStatus);
            Assert.AreEqual(2, recordingProgress.Values[1].ProcessedScheduleCount);
            Assert.AreEqual(2, recordingProgress.Values[1].ScheduleNumber.Value);
            Assert.AreEqual("수강 계획_시간표_01.png", result.ExportedFiles[0].OutputFilePath.FileName);
            Assert.AreEqual("수강 계획_시간표_02.png", result.ExportedFiles[1].OutputFilePath.FileName);
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task ExportCurrentAsyncReturnsAnActionableFailureWhenTheFolderCannotBeCreatedAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        Directory.CreateDirectory(testDirectoryPath);
        string existingFilePath = Path.Combine(testDirectoryPath, "not-a-folder");
        File.WriteAllText(existingFilePath, "occupied");

        try
        {
            ScheduleGridViewModel scheduleGrid = SchedulePngTestData.createScheduleGrid(
                "네트워크",
                EDay.Thursday,
                4);
            SchedulePngExportRequest request = new SchedulePngExportRequest(
                scheduleGrid,
                new ScheduleExportNumber(1),
                new ScheduleExportDirectoryPath(existingFilePath),
                new ScheduleExportBaseName("네트워크"));
            SchedulePngExporter exporter = new SchedulePngExporter();

            SchedulePngExportResult result = await exporter.ExportCurrentAsync(
                request,
                CancellationToken.None);

            Assert.AreEqual(ESchedulePngExportCompletion.Failed, result.Completion);
            Assert.IsEmpty(result.ExportedFiles);
            Assert.HasCount(1, result.Failures);
            StringAssert.Contains(result.Failures[0].Message, "시간표 1번 PNG를 저장하지 못했습니다.");
            StringAssert.Contains(result.Failures[0].Message, existingFilePath);
            StringAssert.Contains(result.Failures[0].Message, "원인:");
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task ExportCurrentAsyncHonorsAPreCanceledTokenAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            ScheduleGridViewModel scheduleGrid = SchedulePngTestData.createScheduleGrid(
                "소프트웨어 공학",
                EDay.Friday,
                2);
            SchedulePngExportRequest request = new SchedulePngExportRequest(
                scheduleGrid,
                new ScheduleExportNumber(1),
                new ScheduleExportDirectoryPath(testDirectoryPath),
                new ScheduleExportBaseName("취소 테스트"));
            SchedulePngExporter exporter = new SchedulePngExporter();

            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                cancellationTokenSource.Cancel();

                await Assert.ThrowsAsync<OperationCanceledException>(
                    () => exporter.ExportCurrentAsync(request, cancellationTokenSource.Token));
            }

            Assert.IsFalse(Directory.Exists(testDirectoryPath));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    private static string createTestDirectoryPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "TimetableGenerator.Tests",
            Guid.NewGuid().ToString("N"));
    }

    private static void deleteTestDirectory(string testDirectoryPath)
    {
        if (Directory.Exists(testDirectoryPath))
        {
            Directory.Delete(testDirectoryPath, true);
        }
    }

    private sealed class RecordingExportProgress : IProgress<SchedulePngExportProgress>
    {
        private readonly List<SchedulePngExportProgress> mValues =
            new List<SchedulePngExportProgress>();

        internal IReadOnlyList<SchedulePngExportProgress> Values
        {
            get
            {
                return mValues.AsReadOnly();
            }
        }

        public void Report(SchedulePngExportProgress value)
        {
            mValues.Add(value);
        }
    }
}
