using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;

using FluentIcons.Avalonia;
using FluentIcons.Common;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Exporting;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleWorkspaceCalendarExportTests
{
    [AvaloniaFact]
    public async Task WindowsExportMenuOffersPngAndGoogleCalendarAsync()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        string exportDirectory = createTemporaryDirectory();
        RecordingGoogleCalendarExporter googleExporter =
            createSuccessfulGoogleExporter();
        RecordingAppleCalendarImporter appleImporter =
            new RecordingAppleCalendarImporter(
                EAppleCalendarRuntimePlatform.Unsupported);
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(
            createServices(
                exportDirectory,
                googleExporter,
                appleImporter));
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            Button exportButton = findRequiredButton(
                workspaceView,
                "ExportScheduleButton");
            MenuFlyout menu = Assert.IsType<MenuFlyout>(exportButton.Flyout);
            Assert.Contains("export-menu", menu.FlyoutPresenterClasses);
            MenuItem pngAction = findRequiredMenuItem(
                menu,
                "ExportPngAction");
            MenuItem appleAction = findRequiredMenuItem(
                menu,
                "ExportAppleCalendarAction");
            MenuItem googleAction = findRequiredMenuItem(
                menu,
                "ExportGoogleCalendarAction");

            Assert.True(pngAction.IsVisible);
            Assert.False(appleAction.IsVisible);
            Assert.True(googleAction.IsVisible);
            Assert.Equal(3, menu.Items.Count);
            Assert.Same(pngAction, menu.Items[0]);
            Assert.Same(appleAction, menu.Items[1]);
            Assert.Same(googleAction, menu.Items[2]);
            menu.ShowAt(exportButton);
            Dispatcher.UIThread.RunJobs();
            assertExportMenuItemPresentation(pngAction, Icon.Image);
            assertExportMenuItemPresentation(
                appleAction,
                Icon.CalendarMonth);
            assertExportMenuItemPresentation(
                googleAction,
                Icon.CalendarMonth);
            menu.Hide();
            Assert.Equal(
                "ExportPngImage",
                AutomationProperties.GetAutomationId(pngAction));
            Assert.Equal(
                "ExportGoogleCalendar",
                AutomationProperties.GetAutomationId(googleAction));
            Assert.Same(workspaceView.ExportPngCommand, pngAction.Command);
            Assert.Same(
                workspaceView.ExportGoogleCalendarCommand,
                googleAction.Command);
            Assert.Equal(
                "현재 계획을 Google 캘린더로 내보내기",
                AutomationProperties.GetName(googleAction));
        }
        finally
        {
            await closeWindowAndDeleteDirectoryAsync(
                window,
                workspaceView,
                exportDirectory);
        }
    }

    [AvaloniaFact]
    public async Task GoogleCalendarExportUsesTheCurrentPlanAndSeoulTimeAsync()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        string exportDirectory = createTemporaryDirectory();
        RecordingGoogleCalendarExporter googleExporter =
            createSuccessfulGoogleExporter();
        RecordingAppleCalendarImporter appleImporter =
            new RecordingAppleCalendarImporter(
                EAppleCalendarRuntimePlatform.Unsupported);
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(
            createServices(
                exportDirectory,
                googleExporter,
                appleImporter));
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(
                workspaceView.ExportGoogleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            GoogleCalendarExportPlan? planOrNull =
                googleExporter.ExportedPlanOrNull;
            Assert.NotNull(planOrNull);
            if (planOrNull == null)
            {
                throw new InvalidOperationException(
                    "The Google Calendar export plan was not recorded.");
            }

            Assert.Equal(workspace.ActivePlan.PlanId, planOrNull.PlanId);
            Assert.Equal(workspace.ActivePlan.Name, planOrNull.CalendarName);
            Assert.Equal("Asia/Seoul", planOrNull.TimeZoneId.Value);
            Assert.Equal(
                TimeSpan.FromHours(9.0),
                planOrNull.TimeZoneId.FindUtcOffset(
                    planOrNull.Events[0].FirstOccurrenceDate,
                    planOrNull.Events[0].StartTime).Value);
            Assert.NotEmpty(planOrNull.Events);
            TextBlock status = findRequiredTextBlock(
                workspaceView,
                "ExportStatusText");
            Assert.Equal(
                "Google 캘린더에 ‘"
                    + workspace.ActivePlan.Name.Value
                    + "’ 일정을 반영했습니다.",
                status.Text);
        }
        finally
        {
            await closeWindowAndDeleteDirectoryAsync(
                window,
                workspaceView,
                exportDirectory);
        }
    }

    [AvaloniaFact]
    public async Task MacExportMenuAndImportUseAPersistentPlanCalendarAsync()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        string exportDirectory = createTemporaryDirectory();
        RecordingGoogleCalendarExporter googleExporter =
            createSuccessfulGoogleExporter();
        RecordingAppleCalendarImporter appleImporter =
            new RecordingAppleCalendarImporter(
                EAppleCalendarRuntimePlatform.MacOS);
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(
            createServices(
                exportDirectory,
                googleExporter,
                appleImporter));
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            Button exportButton = findRequiredButton(
                workspaceView,
                "ExportScheduleButton");
            MenuFlyout menu = Assert.IsType<MenuFlyout>(exportButton.Flyout);
            MenuItem appleAction = findRequiredMenuItem(
                menu,
                "ExportAppleCalendarAction");
            MenuItem googleAction = findRequiredMenuItem(
                menu,
                "ExportGoogleCalendarAction");
            Assert.True(appleAction.IsVisible);
            Assert.True(googleAction.IsVisible);
            Assert.Equal(3, menu.Items.Count);
            Assert.Equal(
                new[]
                {
                    "PNG 이미지",
                    "Apple 캘린더",
                    "Google 캘린더",
                },
                menu.Items
                    .OfType<MenuItem>()
                    .Select(menuItem => menuItem.Header)
                    .Cast<string>()
                    .ToArray());
            Assert.Equal(
                "ExportAppleCalendar",
                AutomationProperties.GetAutomationId(appleAction));
            Assert.Same(
                workspaceView.ExportAppleCalendarCommand,
                appleAction.Command);
            menu.ShowAt(exportButton);
            Dispatcher.UIThread.RunJobs();
            MenuItem pngAction = findRequiredMenuItem(
                menu,
                "ExportPngAction");
            assertExportMenuItemPresentation(pngAction, Icon.Image);
            assertExportMenuItemPresentation(
                appleAction,
                Icon.CalendarMonth);
            assertExportMenuItemPresentation(
                googleAction,
                Icon.CalendarMonth);
            menu.Hide();

            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(
                workspaceView.ExportAppleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            IcsCalendarFilePath? filePathOrNull =
                appleImporter.OpenedFilePathOrNull;
            Assert.NotNull(filePathOrNull);
            if (filePathOrNull == null)
            {
                throw new InvalidOperationException(
                    "The Apple Calendar import path was not recorded.");
            }

            string calendarContent = File.ReadAllText(filePathOrNull.Value);
            Assert.Contains(
                "X-WR-CALNAME:" + workspace.ActivePlan.Name.Value,
                calendarContent,
                StringComparison.Ordinal);
            Assert.Contains(
                "TZID:Asia/Seoul",
                calendarContent,
                StringComparison.Ordinal);
            Assert.Contains(
                "UNTIL=20261220T145959Z",
                calendarContent,
                StringComparison.Ordinal);
            TextBlock status = findRequiredTextBlock(
                workspaceView,
                "ExportStatusText");
            Assert.Equal(
                "Apple 캘린더에서 가져오기를 확인해 주세요.",
                status.Text);
        }
        finally
        {
            await closeWindowAndDeleteDirectoryAsync(
                window,
                workspaceView,
                exportDirectory);
        }
    }

    private static ScheduleExportServices createServices(
        string exportDirectory,
        RecordingGoogleCalendarExporter googleExporter,
        RecordingAppleCalendarImporter appleImporter)
    {
        return new ScheduleExportServices(
            new AvaloniaControlPngExporter(PngExportScale.Create(1.0)),
            googleExporter,
            appleImporter,
            new IcsCalendarFileStore(
                new CalendarExportDirectoryPath(exportDirectory)),
            new FixedCalendarExportClock(
                new DateTimeOffset(
                    2026,
                    8,
                    20,
                    10,
                    30,
                    0,
                    TimeSpan.FromHours(9.0))));
    }

    private static RecordingGoogleCalendarExporter
        createSuccessfulGoogleExporter()
    {
        GoogleCalendarExportResult result =
            GoogleCalendarExportResult.Complete(
                new GoogleCalendarId("test-calendar@group.calendar.google.com"),
                new GoogleCalendarReconciliationResult(1, 0, 0));
        return new RecordingGoogleCalendarExporter(result);
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

    private static Button findRequiredButton(
        Control root,
        string controlName)
    {
        Button? buttonOrNull = root.FindControl<Button>(controlName);
        if (buttonOrNull == null)
        {
            throw new InvalidOperationException(
                "The export action was not found: " + controlName);
        }

        return buttonOrNull;
    }

    private static TextBlock findRequiredTextBlock(
        Control root,
        string controlName)
    {
        TextBlock? textBlockOrNull =
            root.FindControl<TextBlock>(controlName);
        if (textBlockOrNull == null)
        {
            throw new InvalidOperationException(
                "The export status text was not found: " + controlName);
        }

        return textBlockOrNull;
    }

    private static MenuItem findRequiredMenuItem(
        MenuFlyout menu,
        string controlName)
    {
        MenuItem? menuItemOrNull = menu.Items
            .OfType<MenuItem>()
            .SingleOrDefault(
                menuItem => string.Equals(
                    menuItem.Name,
                    controlName,
                    StringComparison.Ordinal));
        if (menuItemOrNull == null)
        {
            throw new InvalidOperationException(
                "The export menu action was not found: " + controlName);
        }

        return menuItemOrNull;
    }

    private static void assertExportMenuItemPresentation(
        MenuItem menuItem,
        Icon expectedIcon)
    {
        Assert.Contains("export-menu-item", menuItem.Classes);
        FluentIcon icon = Assert.IsType<FluentIcon>(menuItem.Icon);
        Assert.Equal(expectedIcon, icon.Icon);
        Assert.Equal(IconVariant.Regular, icon.IconVariant);
        Assert.Equal(IconSize.Size20, icon.IconSize);
        Assert.Equal(20.0, icon.FontSize);
        Assert.Equal(20.0, icon.Width);
        Assert.Equal(20.0, icon.Height);
        Assert.Equal(
            HorizontalAlignment.Center,
            icon.HorizontalAlignment);
        Assert.Equal(
            VerticalAlignment.Center,
            icon.VerticalAlignment);
        Assert.Contains("export-menu-icon", icon.Classes);
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

    private static async Task closeWindowAndDeleteDirectoryAsync(
        Window window,
        ScheduleWorkspaceView workspaceView,
        string exportDirectory)
    {
        window.Close();
        Dispatcher.UIThread.RunJobs();
        await workspaceView.ExportResourceReleaseTask;
        Assert.Null(workspaceView.ExportResourceReleaseExceptionOrNull);
        if (Directory.Exists(exportDirectory))
        {
            Directory.Delete(exportDirectory, true);
        }
    }
}
