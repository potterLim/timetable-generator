using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceCalendarExportTests
{
    [AvaloniaFact]
    public async Task ExportShutdownCancelsTheActiveOperationAndBlocksNewExportsUntilRecoveryAsync()
    {
        ControlledGoogleCalendarExporter googleExporter = new ControlledGoogleCalendarExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, createUnavailableAppleExporter()));
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            ICommand exportCommand = workspaceView.ExportGoogleCalendarCommand;
            exportCommand.Execute(null);
            await googleExporter.ExportStartedTask;

            workspaceView.beginExportShutdown();
            await workspaceView.completeExportShutdownAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, googleExporter.ExportCallCount);

            exportCommand.Execute(null);
            Assert.Equal(1, googleExporter.ExportCallCount);

            workspaceView.cancelExportShutdown();
            exportCommand.Execute(null);
            Assert.Equal(2, googleExporter.ExportCallCount);
            googleExporter.Complete(createSuccessfulGoogleResult());
            await Assert.IsType<AsyncDelegateCommand>(exportCommand).ExecutionTask;
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task TimedOutExportShutdownKeepsTheWindowUsableAfterTheOperationFinishesAsync()
    {
        NonCancellingControlledGoogleCalendarExporter googleExporter = new NonCancellingControlledGoogleCalendarExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, createUnavailableAppleExporter()));
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            ICommand exportCommand = workspaceView.ExportGoogleCalendarCommand;
            Button exportButton = findRequiredButton(workspaceView, "ExportScheduleButton");
            exportCommand.Execute(null);
            await googleExporter.ExportStartedTask;

            workspaceView.beginExportShutdown();
            using (CancellationTokenSource timeoutSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(50.0)))
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => workspaceView.completeExportShutdownAsync(timeoutSource.Token));
            }

            workspaceView.cancelExportShutdown();
            Assert.False(exportButton.IsEnabled);

            googleExporter.Complete(createSuccessfulGoogleResult());
            await Assert.IsType<AsyncDelegateCommand>(exportCommand).ExecutionTask;
            Dispatcher.UIThread.RunJobs();
            Assert.True(exportButton.IsEnabled);

            exportCommand.Execute(null);
            await Assert.IsType<AsyncDelegateCommand>(exportCommand).ExecutionTask;
            Assert.Equal(2, googleExporter.ExportCallCount);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task ExportShutdownContinuesWhenACancellationCallbackThrowsAsync()
    {
        ThrowingCancellationCallbackGoogleCalendarExporter googleExporter = new ThrowingCancellationCallbackGoogleCalendarExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, createUnavailableAppleExporter()));
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            ICommand exportCommand = workspaceView.ExportGoogleCalendarCommand;
            exportCommand.Execute(null);
            await googleExporter.ExportStartedTask;

            workspaceView.beginExportShutdown();
            await workspaceView.completeExportShutdownAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
            workspace.Dispose();
        }
    }
}
