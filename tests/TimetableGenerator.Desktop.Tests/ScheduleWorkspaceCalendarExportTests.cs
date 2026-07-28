using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Exporting;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleWorkspaceCalendarExportTests
{
    private const double MAXIMUM_CENTER_DELTA_DIP = 0.05;

    private static readonly TimeSpan TEST_EXPORT_STATUS_DURATION = TimeSpan.FromMilliseconds(30.0);

    private static readonly TimeSpan TEST_EXPORT_STATUS_WAIT = TimeSpan.FromMilliseconds(150.0);

    [AvaloniaFact]
    public async Task WindowsExportMenuOffersPngAndGoogleCalendarAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        RecordingGoogleCalendarExporter googleExporter = createSuccessfulGoogleExporter();
        RecordingAppleCalendarExporter appleExporter = createUnavailableAppleExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, appleExporter));
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            Button exportButton = findRequiredButton(workspaceView, "ExportScheduleButton");
            MenuFlyout menu = Assert.IsType<MenuFlyout>(exportButton.Flyout);
            Assert.Contains("export-menu", menu.FlyoutPresenterClasses);
            MenuItem pngAction = findRequiredMenuItem(menu, "ExportPngAction");
            MenuItem allPngAction = findRequiredMenuItem(menu, "ExportAllPngAction");
            MenuItem appleAction = findRequiredMenuItem(menu, "ExportAppleCalendarAction");
            MenuItem googleAction = findRequiredMenuItem(menu, "ExportGoogleCalendarAction");

            Assert.True(pngAction.IsVisible);
            Assert.Equal(workspace.HasMultipleRecommendations, allPngAction.IsVisible);
            Assert.False(appleAction.IsVisible);
            Assert.True(googleAction.IsVisible);
            Assert.Equal(4, menu.Items.Count);
            Assert.Same(pngAction, menu.Items[0]);
            Assert.Same(allPngAction, menu.Items[1]);
            Assert.Same(appleAction, menu.Items[2]);
            Assert.Same(googleAction, menu.Items[3]);
            Assert.Empty(menu.Items.OfType<Separator>());
            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                menu.ShowAt(exportButton);
                Dispatcher.UIThread.RunJobs();
                assertExportPngImageIconPresentation(pngAction);
                assertExportAllPngMultipleImageIconPresentation(allPngAction);
                assertAppleCalendarIconPresentation(appleAction);
                assertExportRasterLogoPresentation(
                    googleAction,
                    "ExportGoogleCalendarLogoSlot",
                    "ExportGoogleCalendarLogoImage",
                    24.0,
                    24.0,
                    0.5);
                menu.Hide();
            }
            Assert.Equal("ExportPngImage", AutomationProperties.GetAutomationId(pngAction));
            Assert.Equal("ExportAllPngImages", AutomationProperties.GetAutomationId(allPngAction));
            Assert.Equal("모든 가능한 시간표 PNG로 저장", allPngAction.Header);
            Assert.Equal("모든 가능한 시간표 PNG로 저장", AutomationProperties.GetName(allPngAction));
            Assert.Equal("현재 조건으로 만든 가능한 시간표를 각각 번호가 붙은 PNG 이미지로 저장합니다.", AutomationProperties.GetHelpText(allPngAction));
            Assert.Equal("ExportGoogleCalendar", AutomationProperties.GetAutomationId(googleAction));
            Assert.Same(workspaceView.ExportPngCommand, pngAction.Command);
            Assert.Same(workspaceView.ExportAllPngCommand, allPngAction.Command);
            Assert.Same(workspaceView.ExportGoogleCalendarCommand, googleAction.Command);
            Assert.Equal("Google Calendar로 내보내기", AutomationProperties.GetName(googleAction));
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }

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

            await Task.Delay(TEST_EXPORT_STATUS_WAIT);
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

    [AvaloniaFact]
    public async Task AppleCalendarPermissionProgressPersistsAndPreventsDuplicateExportAsync()
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
            Assert.Equal("Apple 캘린더로 내보내는 중입니다.", statusText.Text);
            Assert.Contains("information", statusToast.Classes);

            command.Execute(null);
            await Task.Delay(TEST_EXPORT_STATUS_WAIT, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(1, appleExporter.ExportCallCount);
            Assert.True(statusToast.IsVisible);
            Assert.Equal("Apple 캘린더로 내보내는 중입니다.", statusText.Text);

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

            await Task.Delay(TEST_EXPORT_STATUS_WAIT);
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
            Point scheduleSurfaceOrigin = scheduleSurfaceOriginOrNull ?? default;
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

    [AvaloniaFact]
    public async Task MacExportMenuAndExportUseTheCurrentPlanCalendarAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        RecordingGoogleCalendarExporter googleExporter = createSuccessfulGoogleExporter();
        RecordingAppleCalendarExporter appleExporter = createSuccessfulAppleExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(createServices(googleExporter, appleExporter));
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            Button exportButton = findRequiredButton(workspaceView, "ExportScheduleButton");
            MenuFlyout menu = Assert.IsType<MenuFlyout>(exportButton.Flyout);
            MenuItem appleAction = findRequiredMenuItem(menu, "ExportAppleCalendarAction");
            MenuItem googleAction = findRequiredMenuItem(menu, "ExportGoogleCalendarAction");
            Assert.True(appleAction.IsVisible);
            Assert.True(googleAction.IsVisible);
            Assert.Equal(4, menu.Items.Count);
            Assert.Equal(
                new[]
                {
                    "현재 시간표 PNG 저장",
                    "모든 가능한 시간표 PNG로 저장",
                    "Apple Calendar로 내보내기",
                    "Google Calendar로 내보내기",
                },
                menu.Items
                    .OfType<MenuItem>()
                    .Select(menuItem => menuItem.Header)
                    .Cast<string>()
                    .ToArray());
            Assert.Equal("ExportAppleCalendar", AutomationProperties.GetAutomationId(appleAction));
            Assert.Same(workspaceView.ExportAppleCalendarCommand, appleAction.Command);
            MenuItem pngAction = findRequiredMenuItem(menu, "ExportPngAction");
            MenuItem allPngAction = findRequiredMenuItem(menu, "ExportAllPngAction");
            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                menu.ShowAt(exportButton);
                Dispatcher.UIThread.RunJobs();
                assertExportPngImageIconPresentation(pngAction);
                assertExportAllPngMultipleImageIconPresentation(allPngAction);
                assertAppleCalendarIconPresentation(appleAction);
                assertExportRasterLogoPresentation(
                    googleAction,
                    "ExportGoogleCalendarLogoSlot",
                    "ExportGoogleCalendarLogoImage",
                    24.0,
                    24.0,
                    0.5);
                menu.Hide();
            }
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(workspaceView.ExportAppleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            CalendarExportDocument? documentOrNull = appleExporter.ExportedDocumentOrNull;
            Assert.NotNull(documentOrNull);
            if (documentOrNull == null)
            {
                throw new InvalidOperationException("The Apple Calendar export document was not recorded.");
            }

            Assert.Equal(workspace.ActivePlan.PlanId, documentOrNull.PlanId);
            Assert.Equal(workspace.ActivePlan.Name, documentOrNull.CalendarName);
            Assert.Equal("Asia/Seoul", documentOrNull.AcademicCalendar.TimeZoneId.Value);
            Assert.NotEmpty(documentOrNull.Events);
            Assert.Contains(documentOrNull.Events, calendarEvent => calendarEvent.Content.Summary == "프로그래밍 I(01)");
            TextBlock status = findRequiredTextBlock(workspaceView, "ExportStatusText");
            Assert.Equal("Apple 캘린더로 내보냈습니다.", status.Text);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }

    private static ScheduleExportServices createServices(IGoogleCalendarExporter googleExporter, IAppleCalendarExporter appleExporter, RecordingGoogleCalendarWebNavigator? googleCalendarNavigatorOrNull = null)
    {
        RecordingGoogleCalendarWebNavigator googleCalendarNavigator;
        if (googleCalendarNavigatorOrNull == null)
        {
            googleCalendarNavigator = new RecordingGoogleCalendarWebNavigator(true);
        }
        else
        {
            googleCalendarNavigator = googleCalendarNavigatorOrNull;
        }

        return new ScheduleExportServices(
            new AvaloniaControlPngExporter(PngExportScale.Create(1.0)),
            googleExporter,
            googleCalendarNavigator,
            appleExporter,
            new FixedCalendarTimeZoneProvider(new CalendarTimeZoneId("Asia/Seoul")));
    }

    private sealed class RecordingGoogleCalendarWebNavigator : IGoogleCalendarWebNavigator
    {
        private readonly bool mOpenResult;

        public int OpenAttemptCount { get; private set; }

        public RecordingGoogleCalendarWebNavigator(bool openResult)
        {
            mOpenResult = openResult;
        }

        public bool TryOpen()
        {
            OpenAttemptCount++;
            return mOpenResult;
        }
    }

    private static RecordingAppleCalendarExporter createUnavailableAppleExporter()
    {
        return new RecordingAppleCalendarExporter(false, AppleCalendarExportResult.Fail(EAppleCalendarExportStatus.Unavailable, "test_unavailable"));
    }

    private static RecordingAppleCalendarExporter createSuccessfulAppleExporter()
    {
        return new RecordingAppleCalendarExporter(true, createSuccessfulAppleResult());
    }

    private static AppleCalendarExportResult createSuccessfulAppleResult()
    {
        AppleCalendarNativeExportResult nativeResult = new AppleCalendarNativeExportResult(new AppleCalendarId("test-apple-calendar"), new PlanName("2026-2학기 시간표"), 1, 0);
        return AppleCalendarExportResult.Complete(nativeResult);
    }

    private static RecordingGoogleCalendarExporter createSuccessfulGoogleExporter()
    {
        return new RecordingGoogleCalendarExporter(createSuccessfulGoogleResult());
    }

    private static GoogleCalendarExportResult createSuccessfulGoogleResult()
    {
        return GoogleCalendarExportResult.Complete(new GoogleCalendarId("test-calendar@group.calendar.google.com"), new PlanName("2026-2학기 시간표"), new GoogleCalendarReconciliationResult(1, 0, 0));
    }

    private static Window showInWindow(ScheduleWorkspaceView workspaceView)
    {
        Window window = new Window();
        window.Width = 1_200.0;
        window.Height = 760.0;
        window.Content = workspaceView;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Button findRequiredButton(Control root, string controlName)
    {
        Button? buttonOrNull = root.FindControl<Button>(controlName);
        if (buttonOrNull == null)
        {
            throw new InvalidOperationException("The export action was not found: " + controlName);
        }

        return buttonOrNull;
    }

    private static TControl findRequiredControl<TControl>(Control root, string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("The required control was not found: " + controlName);
        }

        return controlOrNull;
    }

    private static TextBlock findRequiredTextBlock(Control root, string controlName)
    {
        TextBlock? textBlockOrNull = root.FindControl<TextBlock>(controlName);
        if (textBlockOrNull == null)
        {
            throw new InvalidOperationException("The export status text was not found: " + controlName);
        }

        return textBlockOrNull;
    }

    private static MenuItem findRequiredMenuItem(MenuFlyout menu, string controlName)
    {
        MenuItem? menuItemOrNull = menu.Items
            .OfType<MenuItem>()
            .SingleOrDefault(menuItem => string.Equals(menuItem.Name, controlName, StringComparison.Ordinal));
        if (menuItemOrNull == null)
        {
            throw new InvalidOperationException("The export menu action was not found: " + controlName);
        }

        return menuItemOrNull;
    }

    private static void assertExportPngImageIconPresentation(MenuItem menuItem)
    {
        assertExportRasterLogoPresentation(
            menuItem,
            "ExportPngLogoSlot",
            "ExportPngLogoImage",
            24.0,
            24.0,
            null);
    }

    private static void assertExportAllPngMultipleImageIconPresentation(MenuItem menuItem)
    {
        assertExportMenuItemPresentation(menuItem);
        Grid iconSlot = Assert.IsType<Grid>(menuItem.Icon);
        Assert.Equal("ExportAllPngLogoSlot", iconSlot.Name);
        Assert.Equal(24.0, iconSlot.Width);
        Assert.Equal(24.0, iconSlot.Height);
        Assert.Equal(HorizontalAlignment.Center, iconSlot.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, iconSlot.VerticalAlignment);
        Assert.Contains("export-menu-logo-slot", iconSlot.Classes);

        Image backImage = findRequiredImage(iconSlot, "ExportAllPngBackImage");
        Image middleImage = findRequiredImage(iconSlot, "ExportAllPngMiddleImage");
        Image frontImage = findRequiredImage(iconSlot, "ExportAllPngFrontImage");
        assertStackedPngImagePresentation(backImage, 12.0, new Thickness(1.0, 2.0, 0.0, 0.0), -8.0);
        assertStackedPngImagePresentation(middleImage, 12.0, new Thickness(11.0, 2.0, 0.0, 0.0), 8.0);
        assertStackedPngImagePresentation(frontImage, 16.0, new Thickness(4.0, 8.0, 0.0, 0.0), null);
        Assert.Collection(
            iconSlot.Children,
            child => Assert.Same(backImage, child),
            child => Assert.Same(middleImage, child),
            child => Assert.Same(frontImage, child));
    }

    private static Image findRequiredImage(Grid iconSlot, string imageName)
    {
        Image? imageOrNull = iconSlot.FindControl<Image>(imageName);
        Assert.NotNull(imageOrNull);
        if (imageOrNull == null)
        {
            throw new InvalidOperationException("The stacked PNG export image was not found: " + imageName);
        }

        return imageOrNull;
    }

    private static void assertStackedPngImagePresentation(Image image, double size, Thickness margin, double? rotationAngleOrNull)
    {
        Assert.NotNull(image.Source);
        Assert.Equal(size, image.Width);
        Assert.Equal(size, image.Height);
        Assert.Equal(margin, image.Margin);
        Assert.Equal(HorizontalAlignment.Left, image.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Top, image.VerticalAlignment);
        Assert.Equal(Stretch.Uniform, image.Stretch);
        Assert.Contains("export-menu-logo", image.Classes);

        if (rotationAngleOrNull.HasValue)
        {
            Assert.Equal(RelativePoint.Center, image.RenderTransformOrigin);
            RotateTransform rotation = Assert.IsType<RotateTransform>(image.RenderTransform);
            Assert.Equal(rotationAngleOrNull.Value, rotation.Angle);
        }
        else
        {
            Assert.Null(image.RenderTransform);
        }
    }

    private static Grid assertExportRasterLogoPresentation(
        MenuItem menuItem,
        string slotName,
        string imageName,
        double imageWidth,
        double imageHeight,
        double? verticalTranslationOrNull)
    {
        assertExportMenuItemPresentation(menuItem);
        Grid logoSlot = Assert.IsType<Grid>(menuItem.Icon);
        Assert.Equal(slotName, logoSlot.Name);
        Assert.Equal(24.0, logoSlot.Width);
        Assert.Equal(24.0, logoSlot.Height);
        Assert.Equal(HorizontalAlignment.Center, logoSlot.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, logoSlot.VerticalAlignment);
        Assert.Contains("export-menu-logo-slot", logoSlot.Classes);

        Image? logoImageOrNull = logoSlot.FindControl<Image>(imageName);
        Assert.NotNull(logoImageOrNull);
        if (logoImageOrNull == null)
        {
            throw new InvalidOperationException("The export menu logo image was not found: " + imageName);
        }

        Assert.NotNull(logoImageOrNull.Source);
        Assert.Equal(imageWidth, logoImageOrNull.Width);
        Assert.Equal(imageHeight, logoImageOrNull.Height);
        Assert.Equal(Stretch.Uniform, logoImageOrNull.Stretch);
        Assert.Equal(HorizontalAlignment.Center, logoImageOrNull.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, logoImageOrNull.VerticalAlignment);
        Assert.Contains("export-menu-logo", logoImageOrNull.Classes);
        Assert.Same(logoImageOrNull, Assert.Single(logoSlot.Children));

        if (verticalTranslationOrNull.HasValue)
        {
            TranslateTransform translation = Assert.IsType<TranslateTransform>(logoImageOrNull.RenderTransform);
            Assert.Equal(verticalTranslationOrNull.Value, translation.Y);
        }
        else
        {
            Assert.Null(logoImageOrNull.RenderTransform);
        }

        return logoSlot;
    }

    private static void assertAppleCalendarIconPresentation(MenuItem menuItem)
    {
        Assert.Contains("export-menu-item", menuItem.Classes);
        if (menuItem.IsVisible)
        {
            assertExportMenuItemPresentation(menuItem);
        }

        Grid logoSlot = Assert.IsType<Grid>(menuItem.Icon);
        Assert.Equal("ExportAppleCalendarIconSlot", logoSlot.Name);
        Assert.Equal(24.0, logoSlot.Width);
        Assert.Equal(24.0, logoSlot.Height);
        Assert.Equal(HorizontalAlignment.Center, logoSlot.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, logoSlot.VerticalAlignment);
        Assert.Contains("export-menu-logo-slot", logoSlot.Classes);

        Image? iconImageOrNull = logoSlot.FindControl<Image>("ExportAppleCalendarIconImage");
        Assert.NotNull(iconImageOrNull);
        if (iconImageOrNull == null)
        {
            throw new InvalidOperationException("The Apple Calendar icon image was not found.");
        }

        Assert.NotNull(iconImageOrNull.Source);
        Assert.True(iconImageOrNull.IsVisible);
        Assert.Equal(24.0, iconImageOrNull.Width);
        Assert.Equal(24.0, iconImageOrNull.Height);
        Assert.Equal(Stretch.Uniform, iconImageOrNull.Stretch);
        Assert.Equal(HorizontalAlignment.Center, iconImageOrNull.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, iconImageOrNull.VerticalAlignment);
        Assert.Contains("export-menu-logo", iconImageOrNull.Classes);
        Assert.Null(iconImageOrNull.RenderTransform);
        Assert.Same(iconImageOrNull, Assert.Single(logoSlot.Children));
    }

    private static void assertExportMenuItemPresentation(MenuItem menuItem)
    {
        Assert.Contains("export-menu-item", menuItem.Classes);

        ContentControl iconPresenter = menuItem
            .GetVisualDescendants()
            .OfType<ContentControl>()
            .Single(control => string.Equals(
                control.Name,
                "PART_IconPresenter",
                StringComparison.Ordinal));
        Assert.Equal(24.0, iconPresenter.Width);
        Assert.Equal(24.0, iconPresenter.Height);
        Assert.Equal(HorizontalAlignment.Center, iconPresenter.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, iconPresenter.VerticalAlignment);
        Assert.Equal(HorizontalAlignment.Center, iconPresenter.HorizontalContentAlignment);
        Assert.Equal(VerticalAlignment.Center, iconPresenter.VerticalContentAlignment);

        Point? iconOriginOrNull = iconPresenter.TranslatePoint(new Point(0.0, 0.0), menuItem);
        Assert.NotNull(iconOriginOrNull);
        if (iconOriginOrNull == null)
        {
            throw new InvalidOperationException("The export menu icon geometry could not be resolved.");
        }

        double menuItemCenterY = menuItem.Bounds.Height / 2.0;
        double iconCenterY = iconOriginOrNull.Value.Y + (iconPresenter.Bounds.Height / 2.0);
        double iconCenterDelta = iconCenterY - menuItemCenterY;
        Assert.True(Math.Abs(iconCenterDelta) <= MAXIMUM_CENTER_DELTA_DIP, "Export menu icon center delta=" + iconCenterDelta + ", item height=" + menuItem.Bounds.Height + ", icon top=" + iconOriginOrNull.Value.Y + ", icon height=" + iconPresenter.Bounds.Height + ".");

        string headerText = Assert.IsType<string>(menuItem.Header);
        TextBlock header = menuItem.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(candidate => candidate.Text == headerText);
        Point? headerOriginOrNull = header.TranslatePoint(new Point(0.0, 0.0), menuItem);
        Assert.NotNull(headerOriginOrNull);
        if (headerOriginOrNull == null)
        {
            throw new InvalidOperationException("The export menu header geometry could not be resolved.");
        }

        double headerCenterY = headerOriginOrNull.Value.Y + (header.Bounds.Height / 2.0);
        double headerCenterDelta = headerCenterY - menuItemCenterY;
        Assert.True(Math.Abs(headerCenterDelta) <= MAXIMUM_CENTER_DELTA_DIP, "Export menu header center delta=" + headerCenterDelta + ", item height=" + menuItem.Bounds.Height + ", header top=" + headerOriginOrNull.Value.Y + ", header height=" + header.Bounds.Height + ".");
    }

    private static ThemeVariant[] getProductThemeVariants()
    {
        return new ThemeVariant[]
        {
            ThemeVariant.Light,
            ThemeVariant.Dark,
        };
    }

    private sealed class ControlledGoogleCalendarExporter : IGoogleCalendarExporter
    {
        private readonly TaskCompletionSource mExportStartedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<GoogleCalendarExportResult> mCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ExportStartedTask
        {
            get
            {
                return mExportStartedSource.Task;
            }
        }

        public async Task<GoogleCalendarExportResult> ExportAsync(GoogleCalendarExportPlan plan, ICalendarNameConflictResolver conflictResolver, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(conflictResolver);
            cancellationToken.ThrowIfCancellationRequested();
            mExportStartedSource.TrySetResult();
            return await mCompletionSource.Task.WaitAsync(cancellationToken);
        }

        public void Complete(GoogleCalendarExportResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (mCompletionSource.TrySetResult(result) == false)
            {
                throw new InvalidOperationException("The controlled export already completed.");
            }
        }

        public void CancelPendingExport()
        {
            mCompletionSource.TrySetCanceled();
        }

        public void Dispose()
        {
        }
    }

    private sealed class ControlledAppleCalendarExporter : IAppleCalendarExporter
    {
        private readonly TaskCompletionSource mExportStartedSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<AppleCalendarExportResult> mCompletionSource = new TaskCompletionSource<AppleCalendarExportResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsAvailable
        {
            get
            {
                return true;
            }
        }

        public int ExportCallCount { get; private set; }

        public Task ExportStartedTask
        {
            get
            {
                return mExportStartedSource.Task;
            }
        }

        public async Task<AppleCalendarExportResult> ExportAsync(CalendarExportDocument document, ICalendarNameConflictResolver conflictResolver, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(conflictResolver);
            cancellationToken.ThrowIfCancellationRequested();
            ExportCallCount++;
            mExportStartedSource.TrySetResult();
            return await mCompletionSource.Task.WaitAsync(cancellationToken);
        }

        public void Complete(AppleCalendarExportResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (mCompletionSource.TrySetResult(result) == false)
            {
                throw new InvalidOperationException("The controlled export already completed.");
            }
        }

        public void CancelPendingExport()
        {
            mCompletionSource.TrySetCanceled();
        }
    }

    private sealed class QueueGoogleCalendarExporter : IGoogleCalendarExporter
    {
        private readonly Queue<GoogleCalendarExportResult> mResults;

        public QueueGoogleCalendarExporter(params GoogleCalendarExportResult[] results)
        {
            mResults = new Queue<GoogleCalendarExportResult>(results);
        }

        public Task<GoogleCalendarExportResult> ExportAsync(GoogleCalendarExportPlan plan, ICalendarNameConflictResolver conflictResolver, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(plan);
            ArgumentNullException.ThrowIfNull(conflictResolver);
            cancellationToken.ThrowIfCancellationRequested();
            if (mResults.Count == 0)
            {
                throw new InvalidOperationException("No queued Google Calendar export result remains.");
            }

            return Task.FromResult(mResults.Dequeue());
        }

        public void Dispose()
        {
        }
    }

    private static async Task closeWindowAsync(Window window, ScheduleWorkspaceView workspaceView)
    {
        window.Close();
        Dispatcher.UIThread.RunJobs();
        await workspaceView.ExportResourceReleaseTask;
        Assert.Null(workspaceView.ExportResourceReleaseExceptionOrNull);
    }
}
