using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting;

public sealed partial class SchedulePngBatchWriterTests
{
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
                        await writer.exportAsync(exportBatch, directory, exportHost, CancellationToken.None);
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
            ScheduleBoardPresentation firstCandidate = createCandidatePresentation(planName, "오전 후보", EDay.Monday, new ScheduleTime(9, 0), new ScheduleTime(10, 15));
            ScheduleBoardPresentation secondCandidate = createCandidatePresentation(planName, "주말 오후 후보", EDay.Sunday, new ScheduleTime(15, 0), new ScheduleTime(16, 15));
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
                    await writer.exportAsync(exportBatch, directory, exportHost, CancellationToken.None);
                }

                Assert.Equal(2, exporter.Layouts.Count);
                Assert.Equal(new double[] { 1_596.0, 2_196.0 }, exporter.SurfaceWidths);
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
                        await writer.exportAsync(exportBatch, destinationDirectory, exportHost, CancellationToken.None);
                        destinationDirectory.commit();

                        Assert.Equal(
                            new string[]
                            {
                                SchedulePngFileNameFactory.CreateBatchCandidate(exportBatch.PlanName, new SchedulePngCandidateNumber(1, 2)),
                                SchedulePngFileNameFactory.CreateBatchCandidate(exportBatch.PlanName, new SchedulePngCandidateNumber(2, 2)),
                            },
                            Directory.GetFiles(destinationDirectory.DirectoryPath).Select(Path.GetFileName).OrderBy(fileName => fileName, StringComparer.Ordinal).ToArray());
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
}
