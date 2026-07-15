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

            SchedulePngExportResult firstExportResult = await exporter.ExportCurrentAsync(
                request,
                CancellationToken.None);
            SchedulePngExportResult secondExportResult = await exporter.ExportCurrentAsync(
                request,
                CancellationToken.None);

            Assert.AreEqual(
                ESchedulePngExportCompletion.Succeeded,
                firstExportResult.Completion);
            Assert.AreEqual(
                ESchedulePngExportCompletion.Succeeded,
                secondExportResult.Completion);
            Assert.HasCount(1, firstExportResult.ExportedFiles);
            Assert.HasCount(1, secondExportResult.ExportedFiles);
            Assert.AreEqual(
                "2026 봄학기_시간표_03.png",
                firstExportResult.ExportedFiles[0].OutputFilePath.FileName);
            Assert.AreEqual(
                "2026 봄학기_시간표_03 (2).png",
                secondExportResult.ExportedFiles[0].OutputFilePath.FileName);
            Assert.IsTrue(
                File.Exists(firstExportResult.ExportedFiles[0].OutputFilePath.Value));
            Assert.IsTrue(
                File.Exists(secondExportResult.ExportedFiles[0].OutputFilePath.Value));
            CollectionAssert.AreEqual(
                File.ReadAllBytes(firstExportResult.ExportedFiles[0].OutputFilePath.Value),
                File.ReadAllBytes(secondExportResult.ExportedFiles[0].OutputFilePath.Value));
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

            SchedulePngExportResult exportResult = await exporter.ExportAllAsync(
                request,
                recordingProgress,
                CancellationToken.None);

            Assert.AreEqual(
                ESchedulePngExportCompletion.Succeeded,
                exportResult.Completion);
            Assert.HasCount(2, exportResult.ExportedFiles);
            Assert.IsEmpty(exportResult.Failures);
            Assert.HasCount(2, recordingProgress.Values);
            Assert.IsTrue(recordingProgress.Values[0].Position.IsValid);
            Assert.AreEqual(1, recordingProgress.Values[0].ProcessedScheduleCount);
            Assert.AreEqual(2, recordingProgress.Values[0].TotalScheduleCount);
            Assert.AreEqual(1, recordingProgress.Values[0].ScheduleNumber.Value);
            Assert.AreEqual(
                ESchedulePngExportItemStatus.Succeeded,
                recordingProgress.Values[0].ItemStatus);
            Assert.AreEqual(2, recordingProgress.Values[1].ProcessedScheduleCount);
            Assert.AreEqual(2, recordingProgress.Values[1].ScheduleNumber.Value);
            Assert.AreEqual(
                "수강 계획_시간표_01.png",
                exportResult.ExportedFiles[0].OutputFilePath.FileName);
            Assert.AreEqual(
                "수강 계획_시간표_02.png",
                exportResult.ExportedFiles[1].OutputFilePath.FileName);
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

            SchedulePngExportResult exportResult = await exporter.ExportCurrentAsync(
                request,
                CancellationToken.None);

            Assert.AreEqual(ESchedulePngExportCompletion.Failed, exportResult.Completion);
            Assert.IsEmpty(exportResult.ExportedFiles);
            Assert.HasCount(1, exportResult.Failures);
            Assert.AreEqual(
                "네트워크_시간표_01.png",
                exportResult.Failures[0].RequestedFileName.Value);
            StringAssert.Contains(
                exportResult.Failures[0].Message,
                "시간표 1번 PNG를 저장하지 못했습니다.");
            StringAssert.Contains(exportResult.Failures[0].Message, existingFilePath);
            StringAssert.Contains(exportResult.Failures[0].Message, "원인:");
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

    [TestMethod]
    public async Task ExportAllAsyncRollsBackCompletedFilesWhenCanceledAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            IReadOnlyList<ScheduleGridViewModel> scheduleGrids =
                SchedulePngTestData.createScheduleGrids();
            SchedulePngBatchExportRequest request = new SchedulePngBatchExportRequest(
                scheduleGrids,
                new ScheduleExportDirectoryPath(testDirectoryPath),
                new ScheduleExportBaseName("취소 롤백"));
            SchedulePngExporter exporter = new SchedulePngExporter();

            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                CancelAfterFirstExportProgress cancellationProgress =
                    new CancelAfterFirstExportProgress(cancellationTokenSource);
                SchedulePngExportResult exportResult = await exporter.ExportAllAsync(
                    request,
                    cancellationProgress,
                    cancellationTokenSource.Token);

                Assert.AreEqual(
                    ESchedulePngExportCompletion.Canceled,
                    exportResult.Completion);
                Assert.IsEmpty(exportResult.ExportedFiles);
                Assert.IsEmpty(exportResult.Failures);
            }

            Assert.HasCount(0, Directory.GetFiles(testDirectoryPath, "*.png"));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task ExportAllAsyncRollsBackCompletedFilesWhenProgressReportingFailsAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            IReadOnlyList<ScheduleGridViewModel> scheduleGrids =
                SchedulePngTestData.createScheduleGrids();
            SchedulePngBatchExportRequest request = new SchedulePngBatchExportRequest(
                scheduleGrids,
                new ScheduleExportDirectoryPath(testDirectoryPath),
                new ScheduleExportBaseName("오류 롤백"));
            SchedulePngExporter exporter = new SchedulePngExporter();
            ThrowingExportProgress throwingProgress = new ThrowingExportProgress();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => exporter.ExportAllAsync(
                    request,
                    throwingProgress,
                    CancellationToken.None));

            Assert.HasCount(0, Directory.GetFiles(testDirectoryPath, "*.png"));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task ExportCurrentAsyncRollsBackCompletedFileWhenProgressReportingFailsAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            ScheduleGridViewModel scheduleGrid = SchedulePngTestData.createScheduleGrid(
                "운영체제",
                EDay.Wednesday,
                3);
            SchedulePngExportRequest request = new SchedulePngExportRequest(
                scheduleGrid,
                new ScheduleExportNumber(1),
                new ScheduleExportDirectoryPath(testDirectoryPath),
                new ScheduleExportBaseName("현재 오류 롤백"));
            SchedulePngExporter exporter = new SchedulePngExporter();
            ThrowingExportProgress throwingProgress = new ThrowingExportProgress();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => exporter.ExportCurrentAsync(
                    request,
                    throwingProgress,
                    CancellationToken.None));

            Assert.HasCount(0, Directory.GetFiles(testDirectoryPath, "*.png"));
        }
        finally
        {
            deleteTestDirectory(testDirectoryPath);
        }
    }

    [TestMethod]
    public async Task ExportAllAsyncReportsRetainedArtifactWhenRollbackCannotDeleteAFileAsync()
    {
        string testDirectoryPath = createTestDirectoryPath();
        try
        {
            IReadOnlyList<ScheduleGridViewModel> scheduleGrids =
                SchedulePngTestData.createScheduleGrids();
            SchedulePngBatchExportRequest request = new SchedulePngBatchExportRequest(
                scheduleGrids,
                new ScheduleExportDirectoryPath(testDirectoryPath),
                new ScheduleExportBaseName("잠금 롤백"));
            SchedulePngExporter exporter = new SchedulePngExporter();

            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                using (LockFirstOutputAndCancelProgress progress =
                    new LockFirstOutputAndCancelProgress(
                        testDirectoryPath,
                        cancellationTokenSource))
                {
                    SchedulePngExportCleanupException exception =
                        await Assert.ThrowsAsync<SchedulePngExportCleanupException>(
                            () => exporter.ExportAllAsync(
                                request,
                                progress,
                                cancellationTokenSource.Token));

                    Assert.HasCount(1, exception.RetainedArtifacts);
                    Assert.AreEqual(
                        ESchedulePngExportArtifactKind.CompletedPng,
                        exception.RetainedArtifacts[0].Kind);
                    Assert.AreEqual(
                        Path.Combine(testDirectoryPath, "잠금 롤백_시간표_01.png"),
                        exception.RetainedArtifacts[0].FilePath.Value);
                }
            }
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

        public void Report(SchedulePngExportProgress progress)
        {
            mValues.Add(progress);
        }
    }

    private sealed class CancelAfterFirstExportProgress : IProgress<SchedulePngExportProgress>
    {
        private readonly CancellationTokenSource mCancellationTokenSource;

        internal CancelAfterFirstExportProgress(
            CancellationTokenSource cancellationTokenSource)
        {
            mCancellationTokenSource = cancellationTokenSource;
        }

        public void Report(SchedulePngExportProgress progress)
        {
            if (progress.ProcessedScheduleCount == 1)
            {
                mCancellationTokenSource.Cancel();
            }
        }
    }

    private sealed class ThrowingExportProgress : IProgress<SchedulePngExportProgress>
    {
        public void Report(SchedulePngExportProgress progress)
        {
            throw new InvalidOperationException("Synthetic progress failure.");
        }
    }

    private sealed class LockFirstOutputAndCancelProgress :
        IProgress<SchedulePngExportProgress>,
        IDisposable
    {
        private readonly string mOutputDirectoryPath;
        private readonly CancellationTokenSource mCancellationTokenSource;
        private FileStream? mLockedOutputStreamOrNull;

        internal LockFirstOutputAndCancelProgress(
            string outputDirectoryPath,
            CancellationTokenSource cancellationTokenSource)
        {
            mOutputDirectoryPath = outputDirectoryPath;
            mCancellationTokenSource = cancellationTokenSource;
        }

        public void Report(SchedulePngExportProgress progress)
        {
            if (progress.ProcessedScheduleCount != 1)
            {
                return;
            }

            string firstOutputFilePath = Path.Combine(
                mOutputDirectoryPath,
                "잠금 롤백_시간표_01.png");
            mLockedOutputStreamOrNull = new FileStream(
                firstOutputFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
            mCancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            if (mLockedOutputStreamOrNull != null)
            {
                mLockedOutputStreamOrNull.Dispose();
                mLockedOutputStreamOrNull = null;
            }
        }
    }
}
