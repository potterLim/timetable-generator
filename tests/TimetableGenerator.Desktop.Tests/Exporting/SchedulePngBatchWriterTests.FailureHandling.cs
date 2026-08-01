using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting;

public sealed partial class SchedulePngBatchWriterTests
{
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
                SchedulePngExportBatch exportBatch = new SchedulePngExportBatch(
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
                        SchedulePngBatchExportException exception = await Assert.ThrowsAsync<SchedulePngBatchExportException>(
                            delegate
                            {
                                return writer.exportAsync(exportBatch, directory, sourceBoard, exportHost, CancellationToken.None);
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
                    ResponsiveRecordingPngExporter exporter = new ResponsiveRecordingPngExporter(
                        delegate
                        {
                            return inputCallbackRan;
                        });
                    SchedulePngBatchWriter writer = new SchedulePngBatchWriter(exporter);
                    using (SchedulePngBatchDirectory directory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPath, CancellationToken.None))
                    {
                        await writer.exportAsync(exportBatch, directory, sourceBoard, exportHost, CancellationToken.None);
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
                                    return writer.exportAsync(exportBatch, directory, sourceBoard, exportHost, cancellationSource.Token);
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
}
