using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Exporting;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceCalendarExportTests
{
    [AvaloniaFact]
    public async Task AppleCalendarProgressPersistsAndPreventsDuplicateExportAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ControlledAppleCalendarExporter appleExporter = new ControlledAppleCalendarExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(createSuccessfulGoogleExporter(), appleExporter), TEST_EXPORT_STATUS_DURATION);
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportAppleCalendarCommand);
            command.Execute(null);
            await appleExporter.ExportStartedTask;

            Button exportButton = findRequiredButton(workspaceView, "ExportScheduleButton");
            Border statusToast = findRequiredControl<Border>(workspaceView, "ExportStatusToast");
            TextBlock statusText = findRequiredTextBlock(workspaceView, "ExportStatusText");
            Assert.False(exportButton.IsEnabled);
            Assert.False(command.CanExecute(null));
            Assert.True(statusToast.IsVisible);
            Assert.False(statusToast.IsHitTestVisible);
            Assert.Equal("Apple 캘린더 접근 권한과 기존 일정을 확인하는 중입니다.", statusText.Text);
            Assert.Contains("information", statusToast.Classes);

            command.Execute(null);
            await Task.Delay(TEST_EXPORT_STATUS_WAIT, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, appleExporter.ExportCallCount);
            Assert.True(statusToast.IsVisible);
            Assert.Equal("Apple 캘린더 접근 권한과 기존 일정을 확인하는 중입니다.", statusText.Text);

            appleExporter.Report(EAppleCalendarExportProgressStage.SavingEvents);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Apple 캘린더에 시간표를 저장하는 중입니다.", statusText.Text);

            appleExporter.Report(EAppleCalendarExportProgressStage.Finalizing);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Apple 캘린더 내보내기를 마무리하는 중입니다.", statusText.Text);

            appleExporter.Complete(createSuccessfulAppleResult());
            await command.ExecutionTask;
            Dispatcher.UIThread.RunJobs();

            Assert.True(exportButton.IsEnabled);
            Assert.Equal("Apple 캘린더로 내보냈습니다.", statusText.Text);
            Assert.Contains("success", statusToast.Classes);
        }
        finally
        {
            appleExporter.CancelPendingExport();
            await closeWindowAsync(window, workspaceView);
        }
    }

    [AvaloniaFact]
    public async Task AppleCalendarFailuresProvideActionableProductMessagesAsync()
    {
        (EAppleCalendarExportStatus Status, string DiagnosticCode, string ExpectedMessage)[] cases =
        {
            (
                EAppleCalendarExportStatus.AccessDenied,
                "apple_calendar_access_denied",
                "시스템 설정의 개인정보 보호 및 보안에서 Timetable Generator의 캘린더 접근을 허용해 주세요."),
            (
                EAppleCalendarExportStatus.Failed,
                "apple_calendar_registry_finalize_failed",
                "일정이 저장되었을 수 있습니다. Apple 캘린더에서 확인한 뒤 다시 시도해 주세요."),
            (
                EAppleCalendarExportStatus.Failed,
                "eventkit_reconciliation_ambiguous",
                "Apple 캘린더에서 해당 시간표 캘린더를 확인하고, 중복 일정이 있으면 정리한 뒤 다시 시도해 주세요."),
            (
                EAppleCalendarExportStatus.Failed,
                "eventkit_reconciliation_identifier_changed",
                "이전에 내보낸 시간표를 안전하게 확인할 수 없습니다. Apple 캘린더에서 해당 시간표를 확인한 뒤 다시 시도해 주세요."),
            (
                EAppleCalendarExportStatus.Failed,
                "eventkit_calendar_registration_ambiguous",
                "이전에 내보낸 시간표를 안전하게 확인할 수 없습니다. Apple 캘린더에서 해당 시간표를 확인한 뒤 다시 시도해 주세요."),
            (
                EAppleCalendarExportStatus.Failed,
                "apple_calendar_registry_rebind_failed",
                "Apple 캘린더 연결 정보를 저장하지 못했습니다. 기기의 저장 공간을 확인한 뒤 다시 시도해 주세요."),
            (
                EAppleCalendarExportStatus.Failed,
                "apple_calendar_registry_cleanup_failed",
                "Apple 캘린더 연결 정보를 저장하지 못했습니다. 기기의 저장 공간을 확인한 뒤 다시 시도해 주세요."),
            (
                EAppleCalendarExportStatus.Failed,
                "apple_calendar_registry_load_failed",
                "Apple 캘린더로 내보내지 못했습니다. 다시 시도해 주세요."),
        };

        foreach ((EAppleCalendarExportStatus status, string diagnosticCode, string expectedMessage) in cases)
        {
            PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
            await workspace.RecommendationRefreshTask;
            RecordingAppleCalendarExporter appleExporter = new RecordingAppleCalendarExporter(true, AppleCalendarExportResult.Fail(status, diagnosticCode));
            ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(createSuccessfulGoogleExporter(), appleExporter), TEST_EXPORT_STATUS_DURATION);
            workspaceView.DataContext = workspace;
            Window window = showInWindow(workspaceView);

            try
            {
                AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportAppleCalendarCommand);
                command.Execute(null);
                await command.ExecutionTask;
                Dispatcher.UIThread.RunJobs();

                Border statusToast = findRequiredControl<Border>(workspaceView, "ExportStatusToast");
                TextBlock statusText = findRequiredTextBlock(workspaceView, "ExportStatusText");
                Assert.Equal(expectedMessage, statusText.Text);
                Assert.Contains("error", statusToast.Classes);
            }
            finally
            {
                await closeWindowAsync(window, workspaceView);
            }
        }
    }
}
