using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Exporting;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceCalendarExportTests
{
    private const double MAXIMUM_CENTER_DELTA_DIP = 0.05;

    private static readonly TimeSpan TEST_EXPORT_STATUS_DURATION = TimeSpan.FromMilliseconds(500.0);

    private static readonly TimeSpan TEST_EXPORT_STATUS_WAIT = TimeSpan.FromMilliseconds(150.0);

    private static readonly TimeSpan TEST_EXPORT_STATUS_EXPIRATION_WAIT = TimeSpan.FromMilliseconds(1000.0);

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
                assertExportRasterLogoPresentation(googleAction, "ExportGoogleCalendarLogoSlot", "ExportGoogleCalendarLogoImage", 24.0, 24.0, 0.5);
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
                assertExportRasterLogoPresentation(googleAction, "ExportGoogleCalendarLogoSlot", "ExportGoogleCalendarLogoImage", 24.0, 24.0, 0.5);
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
}
