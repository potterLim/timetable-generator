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
using TimetableGenerator.Domain.Planning;

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
            Directory.CreateDirectory(Path.Combine(
                parentDirectoryPath,
                "2026-2학기 시간표 - 모든 시간표"));
            Directory.CreateDirectory(Path.Combine(
                parentDirectoryPath,
                "2026-2학기 시간표 - 모든 시간표 (2)"));

            using (SchedulePngBatchDirectory directory =
                SchedulePngBatchDirectoryAllocator.createUnique(
                    parentDirectoryPath,
                    new PlanName("2026-2학기 시간표"),
                    CancellationToken.None))
            {
                Assert.Equal(
                    "2026-2학기 시간표 - 모든 시간표 (3)",
                    Path.GetFileName(directory.DirectoryPath));
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
            File.WriteAllText(
                Path.Combine(
                    parentDirectoryPath,
                    "2026-2학기 시간표 - 모든 시간표"),
                "preserve");

            using (SchedulePngBatchDirectory directory =
                SchedulePngBatchDirectoryAllocator.createUnique(
                    parentDirectoryPath,
                    new PlanName("2026-2학기 시간표"),
                    CancellationToken.None))
            {
                Assert.Equal(
                    "2026-2학기 시간표 - 모든 시간표 (2)",
                    Path.GetFileName(directory.DirectoryPath));
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
            string firstFolderName =
                SchedulePngFileNameFactory.CreateBatchFolderName(planName);
            Directory.CreateDirectory(Path.Combine(
                parentDirectoryPath,
                firstFolderName));

            using (SchedulePngBatchDirectory directory =
                SchedulePngBatchDirectoryAllocator.createUnique(
                    parentDirectoryPath,
                    planName,
                    CancellationToken.None))
            {
                string folderName = Path.GetFileName(
                    directory.DirectoryPath);
                Assert.True(
                    System.Text.Encoding.UTF8.GetByteCount(folderName)
                        <= 255);
                Assert.EndsWith(
                    " - 모든 시간표 (2)",
                    folderName,
                    StringComparison.Ordinal);
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
            using (SchedulePngBatchDirectory directory =
                SchedulePngBatchDirectoryAllocator.createUnique(
                    parentDirectoryPath,
                    new PlanName("2026-2학기 시간표"),
                    CancellationToken.None))
            {
                using (Stream stream = directory.createFile("후보.png"))
                {
                    stream.WriteByte(1);
                }

                Assert.Throws<IOException>(
                    delegate
                    {
                        using (Stream stream =
                            directory.createFile("후보.png"))
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
            using (SchedulePngBatchDirectory directory =
                SchedulePngBatchDirectoryAllocator.createUnique(
                    parentDirectoryPath,
                    new PlanName("2026-2학기 시간표"),
                    CancellationToken.None))
            {
                directoryPath = directory.DirectoryPath;
                using (Stream stream = directory.createFile("후보.png"))
                {
                    stream.WriteByte(1);
                }

                File.WriteAllText(
                    Path.Combine(directoryPath, "외부 파일.txt"),
                    "preserve");
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

    [AvaloniaFact]
    public async Task BatchWriterReusesOneAttachedBoardForEveryCandidateAsync()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            using (PlannerWorkspaceViewModel workspace =
                PlannerWorkspaceTestFactory.CreateWorkspace())
            {
                await workspace.RecommendationRefreshTask;
                SchedulePngExportBatch exportBatch = new SchedulePngExportBatch(
                    workspace.PngExportCandidates);
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
                    RecordingPngExporter exporter =
                        new RecordingPngExporter();
                    SchedulePngBatchWriter writer =
                        new SchedulePngBatchWriter(exporter);
                    using (SchedulePngBatchDirectory directory =
                        SchedulePngBatchDirectoryAllocator.createUnique(
                            parentDirectoryPath,
                            exportBatch.PlanName,
                            CancellationToken.None))
                    {
                        await writer.exportAsync(
                            exportBatch,
                            directory,
                            sourceBoard,
                            exportHost,
                            CancellationToken.None);
                    }

                    Assert.Equal(2, exporter.Surfaces.Count);
                    Assert.Same(
                        exporter.Surfaces[0],
                        exporter.Surfaces[1]);
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
    public async Task BatchWriterRendersEveryCandidateWithoutChangingTheViewAsync()
    {
        string parentDirectoryPath = createTemporaryDirectory();
        try
        {
            using (PlannerWorkspaceViewModel workspace =
                PlannerWorkspaceTestFactory.CreateWorkspace())
            {
                await workspace.RecommendationRefreshTask;
                SchedulePngExportBatch exportBatch = new SchedulePngExportBatch(
                    workspace.PngExportCandidates);
                Assert.Equal(2, exportBatch.Candidates.Count);

                ScheduleBoardView sourceBoard = new ScheduleBoardView();
                sourceBoard.Width = 960.0;
                ScheduleBoardPresentation displayedPresentation =
                    Assert.IsType<ScheduleBoardPresentation>(
                        workspace.DisplayedScheduleBoard);
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
                    using (SchedulePngBatchDirectory destinationDirectory =
                        SchedulePngBatchDirectoryAllocator.createUnique(
                            parentDirectoryPath,
                            exportBatch.PlanName,
                            CancellationToken.None))
                    {
                        SchedulePngBatchWriter writer =
                            new SchedulePngBatchWriter(
                                new AvaloniaControlPngExporter(
                                    PngExportScale.Create(1.0)));

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
                        Assert.All(
                            Directory.GetFiles(destinationDirectory.DirectoryPath),
                            assertPngContainsRenderedBoard);
                    }

                    Assert.Same(
                        displayedPresentation,
                        sourceBoard.DataContext);
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
        string path = Path.Combine(
            Path.GetTempPath(),
            "TimetableGeneratorTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void assertPngContainsRenderedBoard(string filePath)
    {
        using (FileStream stream = File.OpenRead(filePath))
        using (Bitmap bitmap = new Bitmap(stream))
        using (WriteableBitmap pixelCopy = new WriteableBitmap(
            bitmap.PixelSize,
            new Vector(96.0, 96.0),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul))
        using (ILockedFramebuffer framebuffer = pixelCopy.Lock())
        {
            bitmap.CopyPixels(framebuffer);
            HashSet<int> sampledColors = new HashSet<int>();
            int horizontalStep = Math.Max(1, bitmap.PixelSize.Width / 96);
            int verticalStep = Math.Max(1, bitmap.PixelSize.Height / 96);
            for (int y = 0;
                y < bitmap.PixelSize.Height;
                y += verticalStep)
            {
                for (int x = 0;
                    x < bitmap.PixelSize.Width;
                    x += horizontalStep)
                {
                    int pixelOffset = (y * framebuffer.RowBytes) + (x * 4);
                    sampledColors.Add(Marshal.ReadInt32(
                        framebuffer.Address,
                        pixelOffset));
                }
            }

            Assert.True(
                sampledColors.Count >= 4,
                "The exported PNG contained only a flat background: "
                    + filePath);
        }
    }

    private sealed class RecordingPngExporter : IControlPngExporter
    {
        private readonly List<Control> mSurfaces = new List<Control>();

        public IReadOnlyList<Control> Surfaces
        {
            get
            {
                return mSurfaces.AsReadOnly();
            }
        }

        public Task ExportControlAsync(
            Control sourceControl,
            Stream destinationStream,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            mSurfaces.Add(sourceControl);
            destinationStream.WriteByte(1);
            return Task.CompletedTask;
        }
    }
}
