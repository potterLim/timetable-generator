using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Exporting;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceCalendarExportTests
{
    [AvaloniaFact]
    public async Task InformationalCalendarCompletionExpiresAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        GoogleCalendarExportResult result = GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.NotConfigured, "test_not_configured");
        RecordingGoogleCalendarExporter googleExporter = new RecordingGoogleCalendarExporter(result);
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, createUnavailableAppleExporter()), TEST_EXPORT_STATUS_DURATION);
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportGoogleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            Border statusToast = findRequiredControl<Border>(workspaceView, "ExportStatusToast");
            TextBlock statusText = findRequiredTextBlock(workspaceView, "ExportStatusText");
            Button dismissButton = findRequiredButton(workspaceView, "DismissExportStatusButton");
            Assert.True(statusToast.IsVisible);
            Assert.False(statusToast.IsHitTestVisible);
            Assert.Equal("Google 캘린더 연결을 아직 사용할 수 없습니다.", statusText.Text);
            Assert.Contains("information", statusToast.Classes);
            Assert.False(dismissButton.IsVisible);

            await Task.Delay(TEST_EXPORT_STATUS_EXPIRATION_WAIT);
            Dispatcher.UIThread.RunJobs();

            Assert.False(statusToast.IsVisible);
            Assert.Equal(string.Empty, statusText.Text);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }

    [AvaloniaFact]
    public async Task CalendarFailureSurvivesAStaleTimerAndSupportsExplicitDismissalAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        QueueGoogleCalendarExporter googleExporter = new QueueGoogleCalendarExporter(createSuccessfulGoogleResult(), GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.NetworkFailed, "test_network_failure"));
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, createUnavailableAppleExporter()), TEST_EXPORT_STATUS_DURATION);
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportGoogleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;
            Assert.Equal("Google 캘린더로 내보냈습니다.", findRequiredTextBlock(workspaceView, "ExportStatusText").Text);

            command.Execute(null);
            await command.ExecutionTask;

            Border statusToast = findRequiredControl<Border>(workspaceView, "ExportStatusToast");
            TextBlock statusText = findRequiredTextBlock(workspaceView, "ExportStatusText");
            Button dismissButton = findRequiredButton(workspaceView, "DismissExportStatusButton");
            Assert.True(statusToast.IsVisible);
            Assert.True(statusToast.IsHitTestVisible);
            Assert.Equal("Google 캘린더에 연결하지 못했습니다. 네트워크를 확인해 주세요.", statusText.Text);
            Assert.Contains("error", statusToast.Classes);
            Assert.True(dismissButton.IsVisible);
            Assert.Equal("내보내기 오류 닫기", AutomationProperties.GetName(dismissButton));

            await Task.Delay(TEST_EXPORT_STATUS_WAIT);
            Dispatcher.UIThread.RunJobs();

            Assert.True(statusToast.IsVisible);
            Assert.Equal("Google 캘린더에 연결하지 못했습니다. 네트워크를 확인해 주세요.", statusText.Text);

            Assert.True(dismissButton.Focus());
            Assert.True(dismissButton.IsFocused);
            dismissButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.False(statusToast.IsVisible);
            Assert.False(statusToast.IsHitTestVisible);
            Assert.Equal(string.Empty, statusText.Text);
            Assert.False(dismissButton.IsVisible);
            Assert.DoesNotContain("error", statusToast.Classes);
            Assert.True(findRequiredButton(workspaceView, "ExportScheduleButton").IsFocused);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }

    [AvaloniaFact]
    public async Task CalendarFailureIgnoresUnrelatedActionsAndClearsAfterContextChangesAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        GoogleCalendarExportResult result = GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.Failed, "test_export_failure");
        RecordingGoogleCalendarExporter googleExporter = new RecordingGoogleCalendarExporter(result);
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, createUnavailableAppleExporter()), TEST_EXPORT_STATUS_DURATION);
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportGoogleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            Border statusToast = findRequiredControl<Border>(workspaceView, "ExportStatusToast");
            TextBlock statusText = findRequiredTextBlock(workspaceView, "ExportStatusText");
            Assert.True(statusToast.IsVisible);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            workspaceView.ToggleSchedulePresentationCommand.Execute(null);
            ScheduleBoardView scheduleBoard = findRequiredControl<ScheduleBoardView>(workspaceView, "ScheduleBoard");
            ScrollViewer scrollViewer = findRequiredControl<ScrollViewer>(scheduleBoard, "ScheduleScrollViewer");
            scrollViewer.Offset = new Vector(0.0, 40.0);
            Grid scheduleSurface = findRequiredControl<Grid>(workspaceView, "ScheduleContentSurface");
            Point? scheduleSurfaceOriginOrNull = scheduleSurface.TranslatePoint(new Point(0.0, 0.0), window);
            Assert.NotNull(scheduleSurfaceOriginOrNull);
            Point scheduleSurfaceOrigin = scheduleSurfaceOriginOrNull.GetValueOrDefault();
            Point emptyClickPosition = new Point(scheduleSurfaceOrigin.X + 8.0, scheduleSurfaceOrigin.Y + scheduleSurface.Bounds.Height - 8.0);
            window.MouseMove(emptyClickPosition, RawInputModifiers.None);
            window.MouseDown(emptyClickPosition, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(emptyClickPosition, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(statusToast.IsVisible);
            Assert.Equal("Google 캘린더에 반영하지 못했습니다. 다시 시도해 주세요.", statusText.Text);

            workspace.ActivePlan = workspace.Plans[1];
            Dispatcher.UIThread.RunJobs();

            Assert.False(statusToast.IsVisible);
            Assert.Equal(string.Empty, statusText.Text);

            workspace.ActivePlan = workspace.Plans[0];
            await workspace.RecommendationRefreshTask;
            command.Execute(null);
            await command.ExecutionTask;
            Assert.True(statusToast.IsVisible);

            workspace.RemoveCourseChoiceGroupCommand.Execute(workspace.ActivePlan.CourseChoiceGroups[0]);
            Dispatcher.UIThread.RunJobs();

            Assert.False(statusToast.IsVisible);
            Assert.Equal(string.Empty, statusText.Text);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }
}
