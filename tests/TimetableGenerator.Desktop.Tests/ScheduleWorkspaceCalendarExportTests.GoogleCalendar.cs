using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
    public async Task GoogleCalendarExportUsesTheCurrentPlanAndSeoulTimeAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        RecordingGoogleCalendarExporter googleExporter = createSuccessfulGoogleExporter();
        RecordingGoogleCalendarWebNavigator googleCalendarNavigator = new RecordingGoogleCalendarWebNavigator(true);
        RecordingAppleCalendarExporter appleExporter = createUnavailableAppleExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, appleExporter, googleCalendarNavigator));
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportGoogleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            GoogleCalendarExportPlan? planOrNull = googleExporter.ExportedPlanOrNull;
            Assert.NotNull(planOrNull);
            if (planOrNull == null)
            {
                throw new InvalidOperationException("The Google Calendar export plan was not recorded.");
            }

            Assert.Equal(workspace.ActivePlan.PlanId, planOrNull.PlanId);
            Assert.Equal(workspace.ActivePlan.Name, planOrNull.CalendarName);
            Assert.Equal("한동대학교 2026-2 시간표입니다.", planOrNull.CalendarDescription.Value);
            Assert.Equal("Asia/Seoul", planOrNull.TimeZoneId.Value);
            Assert.Equal(TimeSpan.FromHours(9.0), planOrNull.TimeZoneId.FindUtcOffset(planOrNull.Events[0].FirstOccurrenceDate, planOrNull.Events[0].StartTime).Value);
            Assert.NotEmpty(planOrNull.Events);
            Assert.Contains(planOrNull.Events, exportEvent => exportEvent.Title == "프로그래밍 I");
            Assert.DoesNotContain(planOrNull.Events, exportEvent => exportEvent.Title == "프로그래밍 I(01)");
            TextBlock status = findRequiredTextBlock(workspaceView, "ExportStatusText");
            Assert.Equal("Google 캘린더로 내보냈습니다.", status.Text);
            Assert.Equal(1, googleCalendarNavigator.OpenAttemptCount);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }

    [AvaloniaFact]
    public async Task GoogleCalendarOpenFailurePreservesSuccessfulExportResultAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        RecordingGoogleCalendarWebNavigator googleCalendarNavigator = new RecordingGoogleCalendarWebNavigator(false);
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(createSuccessfulGoogleExporter(), createUnavailableAppleExporter(), googleCalendarNavigator));
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportGoogleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            Assert.Equal(1, googleCalendarNavigator.OpenAttemptCount);
            TextBlock status = findRequiredTextBlock(workspaceView, "ExportStatusText");
            Assert.Equal("Google 캘린더로 내보냈습니다.", status.Text);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }

    [AvaloniaFact]
    public async Task UnsuccessfulGoogleCalendarExportsDoNotOpenWebCalendarAsync()
    {
        EGoogleCalendarExportStatus[] statuses =
        {
            EGoogleCalendarExportStatus.NotConfigured,
            EGoogleCalendarExportStatus.AuthenticationCancelled,
            EGoogleCalendarExportStatus.AuthenticationFailed,
            EGoogleCalendarExportStatus.AccessDenied,
            EGoogleCalendarExportStatus.NetworkFailed,
            EGoogleCalendarExportStatus.Cancelled,
            EGoogleCalendarExportStatus.Failed,
        };
        foreach (EGoogleCalendarExportStatus exportStatus in statuses)
        {
            PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
            await workspace.RecommendationRefreshTask;
            RecordingGoogleCalendarWebNavigator googleCalendarNavigator = new RecordingGoogleCalendarWebNavigator(true);
            RecordingGoogleCalendarExporter googleExporter = new RecordingGoogleCalendarExporter(GoogleCalendarExportResult.Fail(exportStatus, "test_export_failure"));
            ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, createUnavailableAppleExporter(), googleCalendarNavigator));
            workspaceView.DataContext = workspace;
            Window window = showInWindow(workspaceView);

            try
            {
                AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportGoogleCalendarCommand);
                command.Execute(null);
                await command.ExecutionTask;

                Assert.Equal(0, googleCalendarNavigator.OpenAttemptCount);
            }
            finally
            {
                await closeWindowAsync(window, workspaceView);
            }
        }
    }

    [AvaloniaFact]
    public async Task ExpiredGoogleAuthorizationExplainsThatTheUserCanRetryAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        RecordingGoogleCalendarExporter googleExporter = new RecordingGoogleCalendarExporter(GoogleCalendarExportResult.Fail(EGoogleCalendarExportStatus.AuthenticationFailed, "authorization_timeout"));
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, createUnavailableAppleExporter()));
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportGoogleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            TextBlock status = findRequiredTextBlock(workspaceView, "ExportStatusText");
            Assert.Equal("Google 로그인 시간이 만료되었습니다. 다시 시도해 주세요.", status.Text);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }

    [AvaloniaFact]
    public async Task GoogleCalendarProgressPersistsAndSuccessfulCompletionExpiresAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ControlledGoogleCalendarExporter googleExporter = new ControlledGoogleCalendarExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, createUnavailableAppleExporter()), TEST_EXPORT_STATUS_DURATION);
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            TextBlock statusText = findRequiredTextBlock(workspaceView, "ExportStatusText");
            List<(string? Text, AutomationLiveSetting LiveSetting)> liveRegionTransitions = new List<(string? Text, AutomationLiveSetting LiveSetting)>();
            statusText.PropertyChanged +=
                (object? senderOrNull, AvaloniaPropertyChangedEventArgs eventArgs) =>
                {
                    if (eventArgs.Property == TextBlock.TextProperty)
                    {
                        liveRegionTransitions.Add((statusText.Text, AutomationProperties.GetLiveSetting(statusText)));
                    }
                };
            Assert.True(string.IsNullOrEmpty(statusText.Text));
            Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(statusText));

            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportGoogleCalendarCommand);
            command.Execute(null);
            await googleExporter.ExportStartedTask;

            Border statusToast = findRequiredControl<Border>(workspaceView, "ExportStatusToast");
            Button dismissButton = findRequiredButton(workspaceView, "DismissExportStatusButton");
            Assert.True(statusToast.IsVisible);
            Assert.False(statusToast.IsHitTestVisible);
            Assert.Equal("Google 캘린더로 내보내는 중입니다.", statusText.Text);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(statusText));
            Assert.Contains("information", statusToast.Classes);
            Assert.False(dismissButton.IsVisible);

            await Task.Delay(TEST_EXPORT_STATUS_WAIT);
            Dispatcher.UIThread.RunJobs();

            Assert.True(statusToast.IsVisible);
            Assert.Equal("Google 캘린더로 내보내는 중입니다.", statusText.Text);

            googleExporter.Complete(createSuccessfulGoogleResult());
            await command.ExecutionTask;
            Dispatcher.UIThread.RunJobs();

            Assert.True(statusToast.IsVisible);
            Assert.False(statusToast.IsHitTestVisible);
            Assert.Equal("Google 캘린더로 내보냈습니다.", statusText.Text);
            Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(statusText));
            Assert.Contains("success", statusToast.Classes);
            Assert.False(dismissButton.IsVisible);

            await Task.Delay(TEST_EXPORT_STATUS_EXPIRATION_WAIT);
            Dispatcher.UIThread.RunJobs();

            Assert.False(statusToast.IsVisible);
            Assert.Equal(string.Empty, statusText.Text);
            Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(statusText));
            Assert.NotEmpty(liveRegionTransitions);
            Assert.All(
                liveRegionTransitions,
                static transition => Assert.Equal(
                    string.IsNullOrEmpty(transition.Text)
                        ? AutomationLiveSetting.Off
                        : AutomationLiveSetting.Polite,
                    transition.LiveSetting));
        }
        finally
        {
            googleExporter.CancelPendingExport();
            await closeWindowAsync(window, workspaceView);
        }
    }
}
