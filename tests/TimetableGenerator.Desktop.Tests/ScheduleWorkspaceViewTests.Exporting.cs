using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceViewTests
{
    [AvaloniaFact]
    public void PngExportSurfaceUsesCompleteOutlineAndAddsCanvasPadding()
    {
        ScheduleBoardView scheduleBoard = ScheduleBoardView.createForPngExport();
        Border exportCanvas = Assert.IsType<Border>(scheduleBoard.PngExportSurface);
        Border exportSurface = Assert.IsType<Border>(scheduleBoard.FindControl<Border>("BoardExportSurface"));

        Assert.Equal(new Thickness(0.0), exportCanvas.BorderThickness);
        Assert.Equal(new Thickness(0.0, 0.0, 0.0, 8.0), exportCanvas.Padding);
        Assert.Equal(new Thickness(1.0), exportSurface.BorderThickness);
    }

    [AvaloniaFact]
    public async Task PngExportSurfaceIncludesEveryPeriodAndExpandedCardContentAsync()
    {
        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        entries.Add(createScheduleEntry(EDay.Monday, new AcademicPeriod(1)));
        entries.Add(createLongScheduleEntry(EDay.Thursday, new AcademicPeriod(10)));
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(entries));

        Window window = new Window();
        window.Width = 800.0;
        window.Height = 420.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            ScrollViewer? scrollViewerOrNull = scheduleBoard.FindControl<ScrollViewer>("ScheduleScrollViewer");
            Assert.NotNull(scrollViewerOrNull);
            if (scrollViewerOrNull == null)
            {
                throw new InvalidOperationException("The rendered schedule scroll viewer was not found.");
            }

            Assert.True(scheduleBoard.PngExportSurface.Bounds.Height > scrollViewerOrNull.Viewport.Height);
            Assert.True(scheduleBoard.PngExportSurface.Bounds.Height > 1_002.0);

            AvaloniaControlPngExporter exporter = new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY);
            using (MemoryStream destinationStream = new MemoryStream())
            {
                await exporter.ExportControlAsync(scheduleBoard.PngExportSurface, destinationStream, CancellationToken.None);
                destinationStream.Position = 0L;
                using (Bitmap bitmap = new Bitmap(destinationStream))
                {
                    Assert.True(bitmap.PixelSize.Height > 2_004);
                }
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task CancelingPngSaveClearsPreviousExportStatusAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView();
        workspaceView.DataContext = workspace;

        Window window = new Window();
        window.Width = 1_100.0;
        window.Height = 720.0;
        window.Content = workspaceView;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            TextBlock? exportStatusOrNull = workspaceView.FindControl<TextBlock>("ExportStatusText");
            Border? exportStatusToastOrNull = workspaceView.FindControl<Border>("ExportStatusToast");
            Assert.NotNull(exportStatusOrNull);
            Assert.NotNull(exportStatusToastOrNull);
            if (exportStatusOrNull == null || exportStatusToastOrNull == null)
            {
                throw new InvalidOperationException("The PNG export status was not found.");
            }

            TextBlock exportStatus = exportStatusOrNull;
            Border exportStatusToast = exportStatusToastOrNull;
            exportStatus.Text = "PNG 이미지로 저장했습니다.";
            exportStatus.Classes.Set("success", true);
            exportStatusToast.IsVisible = true;
            exportStatusToast.Classes.Set("success", true);

            AsyncDelegateCommand exportCommand = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportPngCommand);
            exportCommand.Execute(null);
            await exportCommand.ExecutionTask;
            Dispatcher.UIThread.RunJobs();

            Assert.False(exportStatusToast.IsVisible);
            Assert.Equal(string.Empty, exportStatus.Text);
            Assert.DoesNotContain("success", exportStatus.Classes);
            Assert.DoesNotContain("error", exportStatus.Classes);
            Assert.DoesNotContain("success", exportStatusToast.Classes);
            Assert.DoesNotContain("error", exportStatusToast.Classes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ExportActionIsAvailableForARenderedScheduleAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView();
        workspaceView.DataContext = workspace;

        Window window = new Window();
        window.Width = 1_100.0;
        window.Height = 720.0;
        window.Content = workspaceView;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button? exportButtonOrNull = workspaceView.FindControl<Button>("ExportScheduleButton");
            Assert.NotNull(exportButtonOrNull);
            if (exportButtonOrNull == null)
            {
                throw new InvalidOperationException("The schedule export action was not found.");
            }

            Button exportButton = exportButtonOrNull;
            Assert.True(exportButton.IsEnabled);
            Assert.Equal("시간표 내보내기", AutomationProperties.GetName(exportButton));
            Assert.Equal("현재 시간표를 내보내거나 가능한 시간표를 모두 PNG 이미지로 저장합니다.", AutomationProperties.GetHelpText(exportButton));
            Assert.NotNull(exportButton.Flyout);
        }
        finally
        {
            window.Close();
        }
    }

}
