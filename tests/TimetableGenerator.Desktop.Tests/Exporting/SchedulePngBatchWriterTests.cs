using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting;

public sealed class SchedulePngBatchWriterTests
{
    [Fact]
    public void BatchDirectoryAllocatorNeverReusesAnExistingFolder()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(parentDirectoryPath, "2026-2학기 시간표"));
            Directory.CreateDirectory(Path.Combine(parentDirectoryPath, "2026-2학기 시간표 (2)"));

            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createUnique(parentDirectoryPath, new PlanName("2026-2학기 시간표"), CancellationToken.None))
            {
                Assert.Equal("2026-2학기 시간표 (3)", Path.GetFileName(directory.DirectoryPath));
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void BatchDirectoryAllocatorSkipsAnExistingFile()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(parentDirectoryPath, "2026-2학기 시간표"), "preserve");

            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createUnique(parentDirectoryPath, new PlanName("2026-2학기 시간표"), CancellationToken.None))
            {
                Assert.Equal("2026-2학기 시간표 (2)", Path.GetFileName(directory.DirectoryPath));
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void BatchDirectoryAllocatorKeepsCopySuffixWithinComponentLimit()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            PlanName planName = new PlanName(new string('한', 80));
            string firstFolderName = SchedulePngFileNameFactory.CreateBatchFolderName(planName);
            Directory.CreateDirectory(Path.Combine(parentDirectoryPath, firstFolderName));

            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createUnique(parentDirectoryPath, planName, CancellationToken.None))
            {
                string folderName = Path.GetFileName(directory.DirectoryPath);
                Assert.True(System.Text.Encoding.UTF8.GetByteCount(folderName) <= 255);
                Assert.EndsWith(" (2)", folderName, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void BatchDirectoryNeverOverwritesAnExistingFile()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createUnique(parentDirectoryPath, new PlanName("2026-2학기 시간표"), CancellationToken.None))
            {
                using (Stream stream = directory.createFile("후보.png"))
                {
                    stream.WriteByte(1);
                }

                Assert.Throws<IOException>(
                    delegate
                    {
                        using (Stream stream = directory.createFile("후보.png"))
                        {
                        }
                    });
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void UncommittedBatchDirectoryRemovesOnlyItsOwnFiles()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        string directoryPath;
        try
        {
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createUnique(parentDirectoryPath, new PlanName("2026-2학기 시간표"), CancellationToken.None))
            {
                directoryPath = directory.DirectoryPath;
                using (Stream stream = directory.createFile("후보.png"))
                {
                    stream.WriteByte(1);
                }

                File.WriteAllText(Path.Combine(directoryPath, "외부 파일.txt"), "preserve");
            }

            Assert.True(Directory.Exists(directoryPath));
            Assert.False(File.Exists(Path.Combine(directoryPath, "후보.png")));
            Assert.True(File.Exists(Path.Combine(directoryPath, "외부 파일.txt")));
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void StagedBatchBecomesVisibleOnlyAfterItsAtomicCommit()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            PlanName planName = new PlanName("2026-2학기 시간표");
            string stagingDirectoryPath;
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
            {
                stagingDirectoryPath = directory.DirectoryPath;
                Assert.StartsWith(".timetable-generator-png-staging-", Path.GetFileName(stagingDirectoryPath), StringComparison.Ordinal);
                using (Stream stream = directory.createFile("후보.png"))
                {
                    stream.WriteByte(1);
                }

                string finalDirectoryPath = Path.Combine(parentDirectoryPath, SchedulePngFileNameFactory.CreateBatchFolderName(planName));
                Assert.False(Directory.Exists(finalDirectoryPath));

                directory.commitAsUniqueBatch(planName, CancellationToken.None);

                Assert.False(Directory.Exists(stagingDirectoryPath));
                Assert.Equal(finalDirectoryPath, directory.DirectoryPath);
                Assert.True(Directory.Exists(finalDirectoryPath));
                Assert.True(File.Exists(Path.Combine(finalDirectoryPath, "후보.png")));
                Assert.False(File.Exists(Path.Combine(finalDirectoryPath, ".timetable-generator-exporting")));
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [Fact]
    public void UncommittedStagingDirectoryRemovesEveryPartialResult()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        string stagingDirectoryPath;
        try
        {
            using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
            {
                stagingDirectoryPath = directory.DirectoryPath;
                using (Stream stream = directory.createFile("부분 결과.png"))
                {
                    stream.WriteByte(1);
                }

                Assert.True(Directory.Exists(stagingDirectoryPath));
            }

            Assert.False(Directory.Exists(stagingDirectoryPath));
            Assert.Empty(Directory.GetFileSystemEntries(parentDirectoryPath));
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [AvaloniaFact]
    public async Task BatchWriterReusesOneAttachedBoardForEveryCandidateAsync()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
            {
                await workspace.RecommendationRefreshTask;
                SchedulePngExportBatch exportBatch = new SchedulePngExportBatch(workspace.PngExportCandidates);
                ScheduleBoardView sourceBoard = new ScheduleBoardView();
                sourceBoard.Width = 960.0;
                sourceBoard.DataContext = workspace.DisplayedScheduleBoard;
                Canvas exportHost = new Canvas();
                Grid root = new Grid();
                root.Children.Add(exportHost);
                root.Children.Add(sourceBoard);
                Window window = new Window();
                window.Width = 1_000.0;
                window.Height = 720.0;
                window.Content = root;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                try
                {
                    RecordingPngExporter exporter = new RecordingPngExporter();
                    SchedulePngBatchWriter writer = new SchedulePngBatchWriter(exporter);
                    using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createUnique(parentDirectoryPath, exportBatch.PlanName, CancellationToken.None))
                    {
                        await writer.exportAsync(exportBatch, directory, sourceBoard, exportHost, CancellationToken.None);
                    }

                    Assert.Equal(2, exporter.Surfaces.Count);
                    Assert.Same(exporter.Surfaces[0], exporter.Surfaces[1]);
                    Assert.Empty(exportHost.Children);
                }
                finally
                {
                    window.Close();
                }
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [AvaloniaFact]
    public async Task BatchWriterRecalculatesTheLayoutForEveryCandidateAsync()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            PlanName planName = new PlanName("후보별 PNG 축 테스트");
            ScheduleBoardPresentation firstCandidate =
                createCandidatePresentation(
                    planName,
                    "오전 후보",
                    EDay.Monday,
                    new ScheduleTime(9, 0),
                    new ScheduleTime(10, 15));
            ScheduleBoardPresentation secondCandidate =
                createCandidatePresentation(
                    planName,
                    "주말 오후 후보",
                    EDay.Sunday,
                    new ScheduleTime(15, 0),
                    new ScheduleTime(16, 15));
            SchedulePngExportBatch exportBatch = new SchedulePngExportBatch(
                new ScheduleBoardPresentation[]
                {
                    firstCandidate,
                    secondCandidate,
                });
            ScheduleBoardView sourceBoard = new ScheduleBoardView();
            sourceBoard.Width = 960.0;
            sourceBoard.DataContext = firstCandidate;
            Canvas exportHost = new Canvas();
            Grid root = new Grid();
            root.Children.Add(exportHost);
            root.Children.Add(sourceBoard);
            Window window = new Window();
            window.Width = 1_000.0;
            window.Height = 720.0;
            window.Content = root;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            try
            {
                RecordingPngExporter exporter = new RecordingPngExporter();
                SchedulePngBatchWriter writer = new SchedulePngBatchWriter(exporter);
                using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createUnique(parentDirectoryPath, exportBatch.PlanName, CancellationToken.None))
                {
                    await writer.exportAsync(exportBatch, directory, sourceBoard, exportHost, CancellationToken.None);
                }

                Assert.Equal(2, exporter.Layouts.Count);
                ScheduleBoardLayout firstLayout = exporter.Layouts[0];
                Assert.Equal(5, firstLayout.DayRange.DayCount);
                Assert.Equal(new ScheduleBoardTimeBoundary(510), firstLayout.TimeAxis.Start);
                Assert.Equal(new ScheduleBoardTimeBoundary(630), firstLayout.TimeAxis.End);
                Assert.DoesNotContain(firstLayout.TimeAxis.End, firstLayout.TimeAxis.LabelTimes);

                ScheduleBoardLayout secondLayout = exporter.Layouts[1];
                Assert.Equal(7, secondLayout.DayRange.DayCount);
                Assert.Equal(new ScheduleBoardTimeBoundary(870), secondLayout.TimeAxis.Start);
                Assert.Equal(new ScheduleBoardTimeBoundary(990), secondLayout.TimeAxis.End);
                Assert.DoesNotContain(secondLayout.TimeAxis.End, secondLayout.TimeAxis.LabelTimes);
                Assert.Same(firstCandidate, sourceBoard.DataContext);
                Assert.Empty(exportHost.Children);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [AvaloniaFact]
    public async Task BatchWriterReportsEveryCandidateResultWithoutCommittingPartialsAsync()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        string? stagingDirectoryPathOrNull = null;
        try
        {
            using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
            {
                await workspace.RecommendationRefreshTask;
                ScheduleBoardPresentation firstCandidate = workspace.PngExportCandidates[0];
                ScheduleBoardPresentation secondCandidate = workspace.PngExportCandidates[1];
                SchedulePngExportBatch exportBatch =
                    new SchedulePngExportBatch(
                        new ScheduleBoardPresentation[]
                        {
                            firstCandidate,
                            secondCandidate,
                            firstCandidate,
                        });
                ScheduleBoardView sourceBoard = new ScheduleBoardView();
                sourceBoard.Width = 960.0;
                sourceBoard.DataContext = firstCandidate;
                Canvas exportHost = new Canvas();
                Grid root = new Grid();
                root.Children.Add(exportHost);
                root.Children.Add(sourceBoard);
                Window window = new Window();
                window.Width = 1_000.0;
                window.Height = 720.0;
                window.Content = root;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                try
                {
                    FailingCandidatePngExporter exporter = new FailingCandidatePngExporter(2);
                    SchedulePngBatchWriter writer = new SchedulePngBatchWriter(exporter);
                    using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
                    {
                        stagingDirectoryPathOrNull = directory.DirectoryPath;
                        SchedulePngBatchExportException exception =
                            await Assert.ThrowsAsync<SchedulePngBatchExportException>(
                                    delegate
                                    {
                                        return writer.exportAsync(
                                            exportBatch,
                                            directory,
                                            sourceBoard,
                                            exportHost,
                                            CancellationToken.None);
                                    });

                        Assert.Equal(2, exception.SuccessfulCount);
                        Assert.Equal(1, exception.FailedCount);
                        Assert.Equal(3, exporter.ExportCallCount);
                        Assert.True(Directory.Exists(stagingDirectoryPathOrNull));
                        Assert.False(Directory.Exists(Path.Combine(parentDirectoryPath, SchedulePngFileNameFactory.CreateBatchFolderName(exportBatch.PlanName))));
                    }
                }
                finally
                {
                    window.Close();
                }
            }

            Assert.NotNull(stagingDirectoryPathOrNull);
            Assert.False(Directory.Exists(stagingDirectoryPathOrNull));
            Assert.Empty(Directory.GetFileSystemEntries(parentDirectoryPath));
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [AvaloniaFact]
    public async Task LargeSynchronousBatchYieldsToTheUiDispatcherAsync()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
            {
                await workspace.RecommendationRefreshTask;
                ScheduleBoardPresentation firstCandidate = workspace.PngExportCandidates[0];
                ScheduleBoardPresentation secondCandidate = workspace.PngExportCandidates[1];
                List<ScheduleBoardPresentation> candidates = new List<ScheduleBoardPresentation>();
                for (int candidateIndex = 0; candidateIndex < 64; ++candidateIndex)
                {
                    candidates.Add(candidateIndex % 2 == 0 ? firstCandidate : secondCandidate);
                }

                SchedulePngExportBatch exportBatch = new SchedulePngExportBatch(candidates);
                ScheduleBoardView sourceBoard = new ScheduleBoardView();
                sourceBoard.Width = 960.0;
                sourceBoard.DataContext = firstCandidate;
                Canvas exportHost = new Canvas();
                Grid root = new Grid();
                root.Children.Add(exportHost);
                root.Children.Add(sourceBoard);
                Window window = new Window();
                window.Width = 1_000.0;
                window.Height = 720.0;
                window.Content = root;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                try
                {
                    bool inputCallbackRan = false;
                    Dispatcher.UIThread.Post(
                        delegate
                        {
                            inputCallbackRan = true;
                        },
                        DispatcherPriority.Input);
                    ResponsiveRecordingPngExporter exporter =
                        new ResponsiveRecordingPngExporter(
                            delegate
                            {
                                return inputCallbackRan;
                            });
                    SchedulePngBatchWriter writer = new SchedulePngBatchWriter(exporter);
                    using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
                    {
                        await writer.exportAsync(
                            exportBatch,
                            directory,
                            sourceBoard,
                            exportHost,
                            CancellationToken.None);
                    }

                    Assert.True(inputCallbackRan);
                    Assert.True(exporter.InputWasResponsiveDuringBatch);
                    Assert.Equal(exportBatch.Candidates.Count, exporter.ExportCallCount);
                }
                finally
                {
                    window.Close();
                }
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [AvaloniaFact]
    public async Task CancellationRemovesTheWholeStagingDirectoryAsync()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        string? stagingDirectoryPathOrNull = null;
        try
        {
            using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
            {
                await workspace.RecommendationRefreshTask;
                SchedulePngExportBatch exportBatch = new SchedulePngExportBatch(workspace.PngExportCandidates);
                ScheduleBoardView sourceBoard = new ScheduleBoardView();
                sourceBoard.Width = 960.0;
                sourceBoard.DataContext = workspace.PngExportCandidates[0];
                Canvas exportHost = new Canvas();
                Grid root = new Grid();
                root.Children.Add(exportHost);
                root.Children.Add(sourceBoard);
                Window window = new Window();
                window.Width = 1_000.0;
                window.Height = 720.0;
                window.Content = root;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                try
                {
                    using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
                    {
                        CancellingPngExporter exporter = new CancellingPngExporter(cancellationSource);
                        SchedulePngBatchWriter writer = new SchedulePngBatchWriter(exporter);
                        using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, cancellationSource.Token))
                        {
                            stagingDirectoryPathOrNull = directory.DirectoryPath;
                            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                                    delegate
                                    {
                                        return writer.exportAsync(
                                            exportBatch,
                                            directory,
                                            sourceBoard,
                                            exportHost,
                                            cancellationSource.Token);
                                    });
                        }
                    }
                }
                finally
                {
                    window.Close();
                }
            }

            Assert.NotNull(stagingDirectoryPathOrNull);
            Assert.False(Directory.Exists(stagingDirectoryPathOrNull));
            Assert.Empty(Directory.GetFileSystemEntries(parentDirectoryPath));
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    [AvaloniaFact]
    public async Task BatchWriterRendersEveryCandidateWithoutChangingTheViewAsync()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
            {
                await workspace.RecommendationRefreshTask;
                SchedulePngExportBatch exportBatch = new SchedulePngExportBatch(workspace.PngExportCandidates);
                Assert.Equal(2, exportBatch.Candidates.Count);

                ScheduleBoardView sourceBoard = new ScheduleBoardView();
                sourceBoard.Width = 960.0;
                ScheduleBoardPresentation displayedPresentation = Assert.IsType<ScheduleBoardPresentation>(workspace.DisplayedScheduleBoard);
                sourceBoard.DataContext = displayedPresentation;
                Canvas exportHost = new Canvas();
                exportHost.IsHitTestVisible = false;
                exportHost.Opacity = 0.0;
                Grid root = new Grid();
                root.Children.Add(exportHost);
                root.Children.Add(sourceBoard);
                Window window = new Window();
                window.Width = 1_000.0;
                window.Height = 720.0;
                window.RequestedThemeVariant = ThemeVariant.Light;
                window.Content = root;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                try
                {
                    using (SchedulePngBatchDirectory destinationDirectory = SchedulePngBatchDirectoryAllocator.createUnique(parentDirectoryPath, exportBatch.PlanName, CancellationToken.None))
                    {
                        SchedulePngBatchWriter writer = new SchedulePngBatchWriter(new AvaloniaControlPngExporter(PngExportScale.Create(1.0)));

                        await writer.exportAsync(
                            exportBatch,
                            destinationDirectory,
                            sourceBoard,
                            exportHost,
                            CancellationToken.None);
                        destinationDirectory.commit();

                        Assert.Equal(
                            new string[]
                            {
                            SchedulePngFileNameFactory.CreateBatchCandidate(
                                exportBatch.PlanName,
                                new SchedulePngCandidateNumber(1, 2)),
                            SchedulePngFileNameFactory.CreateBatchCandidate(
                                exportBatch.PlanName,
                                new SchedulePngCandidateNumber(2, 2)),
                            },
                                Directory.GetFiles(destinationDirectory.DirectoryPath)
                                    .Select(Path.GetFileName)
                                    .OrderBy(
                                        fileName => fileName,
                                        StringComparer.Ordinal)
                                    .ToArray());
                        Assert.All(Directory.GetFiles(destinationDirectory.DirectoryPath), assertPngContainsRenderedBoard);
                    }

                    Assert.Same(displayedPresentation, sourceBoard.DataContext);
                    Assert.Empty(exportHost.Children);
                }
                finally
                {
                    window.Close();
                }
            }
        }
        finally
        {
            Directory.Delete(parentDirectoryPath, true);
        }
    }

    private static string createTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "TimetableGeneratorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ScheduleBoardPresentation createCandidatePresentation(
        PlanName planName,
        string title,
        EDay day,
        ScheduleTime start,
        ScheduleTime end)
    {
        DailyTimeRange dailyTimeRange = new DailyTimeRange(start, end);
        WeeklyTimeRange weeklyTimeRange = new WeeklyTimeRange(day, dailyTimeRange);
        PersonalSchedule personalSchedule = new PersonalSchedule(PersonalScheduleId.CreateNew(), new PersonalScheduleTitle(title), new WeeklyTimeRange[] { weeklyTimeRange }, PersonalScheduleDetails.CreateEmpty());
        PersonalScheduleEntry entry = new PersonalScheduleEntry(personalSchedule, weeklyTimeRange);
        return new ScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { entry }), planName, new InstitutionName("한동대학교"), AcademicTerm.Parse("2026-2"));
    }

    private static void assertPngContainsRenderedBoard(string filePath)
    {
        using (FileStream stream = File.OpenRead(filePath))
        using (Bitmap bitmap = new Bitmap(stream))
        using (WriteableBitmap pixelCopy = new WriteableBitmap(bitmap.PixelSize, new Vector(96.0, 96.0), PixelFormat.Bgra8888, AlphaFormat.Premul))
        using (ILockedFramebuffer framebuffer = pixelCopy.Lock())
        {
            bitmap.CopyPixels(framebuffer);
            HashSet<int> sampledColors = new HashSet<int>();
            int horizontalStep = Math.Max(1, bitmap.PixelSize.Width / 96);
            int verticalStep = Math.Max(1, bitmap.PixelSize.Height / 96);
            for (int y = 0; y < bitmap.PixelSize.Height; y += verticalStep)
            {
                for (int x = 0; x < bitmap.PixelSize.Width; x += horizontalStep)
                {
                    int pixelOffset = (y * framebuffer.RowBytes) + (x * 4);
                    sampledColors.Add(Marshal.ReadInt32(framebuffer.Address, pixelOffset));
                }
            }

            Assert.True(sampledColors.Count >= 4, "The exported PNG contained only a flat background: " + filePath);
        }
    }

    private sealed class FailingCandidatePngExporter : IControlPngExporter
    {
        private readonly int mFailingCallNumber;

        public int ExportCallCount { get; private set; }

        public FailingCandidatePngExporter(int failingCallNumber)
        {
            mFailingCallNumber = failingCallNumber;
        }

        public Task ExportControlAsync(Control sourceControl, Stream destinationStream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExportCallCount++;
            destinationStream.WriteByte(1);
            if (ExportCallCount == mFailingCallNumber)
            {
                throw new IOException("Synthetic candidate export failure.");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ResponsiveRecordingPngExporter
        : IControlPngExporter
    {
        private readonly Func<bool> mReadInputResponsiveness;

        public int ExportCallCount { get; private set; }

        public bool InputWasResponsiveDuringBatch { get; private set; }

        public ResponsiveRecordingPngExporter(Func<bool> readInputResponsiveness)
        {
            mReadInputResponsiveness = readInputResponsiveness ?? throw new ArgumentNullException(nameof(readInputResponsiveness));
        }

        public Task ExportControlAsync(Control sourceControl, Stream destinationStream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExportCallCount++;
            InputWasResponsiveDuringBatch |= mReadInputResponsiveness();

            destinationStream.WriteByte(1);
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingPngExporter : IControlPngExporter
    {
        private readonly CancellationTokenSource mCancellationSource;

        public CancellingPngExporter(CancellationTokenSource cancellationSource)
        {
            mCancellationSource = cancellationSource ?? throw new ArgumentNullException(nameof(cancellationSource));
        }

        public Task ExportControlAsync(Control sourceControl, Stream destinationStream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            destinationStream.WriteByte(1);
            mCancellationSource.Cancel();
            return Task.FromCanceled(mCancellationSource.Token);
        }
    }

    private sealed class RecordingPngExporter : IControlPngExporter
    {
        private readonly List<Control> mSurfaces = new List<Control>();

        private readonly List<ScheduleBoardLayout> mLayouts = new List<ScheduleBoardLayout>();

        public IReadOnlyList<Control> Surfaces
        {
            get
            {
                return mSurfaces.AsReadOnly();
            }
        }

        public IReadOnlyList<ScheduleBoardLayout> Layouts
        {
            get
            {
                return mLayouts.AsReadOnly();
            }
        }

        public Task ExportControlAsync(Control sourceControl, Stream destinationStream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mSurfaces.Add(sourceControl);
            ScheduleBoardPresentation presentation = Assert.IsType<ScheduleBoardPresentation>(sourceControl.DataContext);
            mLayouts.Add(presentation.Layout);
            destinationStream.WriteByte(1);
            return Task.CompletedTask;
        }
    }
}
