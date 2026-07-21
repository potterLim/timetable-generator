using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

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
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleWorkspaceCalendarExportTests
{
    private const double MAXIMUM_CENTER_DELTA_DIP = 0.05;

    [AvaloniaFact]
    public async Task WindowsExportMenuOffersPngAndGoogleCalendarAsync()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        RecordingGoogleCalendarExporter googleExporter =
            createSuccessfulGoogleExporter();
        RecordingAppleCalendarExporter appleExporter =
            createUnavailableAppleExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(
            createServices(
                googleExporter,
                appleExporter));
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
            MenuItem allPngAction = findRequiredMenuItem(
                menu,
                "ExportAllPngAction");
            MenuItem appleAction = findRequiredMenuItem(
                menu,
                "ExportAppleCalendarAction");
            MenuItem googleAction = findRequiredMenuItem(
                menu,
                "ExportGoogleCalendarAction");

            Assert.True(pngAction.IsVisible);
            Assert.Equal(
                workspace.HasMultipleRecommendations,
                allPngAction.IsVisible);
            Assert.False(appleAction.IsVisible);
            Assert.True(googleAction.IsVisible);
            Assert.Equal(5, menu.Items.Count);
            Assert.Same(pngAction, menu.Items[0]);
            Assert.Same(appleAction, menu.Items[1]);
            Assert.Same(googleAction, menu.Items[2]);
            Separator allPngSeparator = Assert.IsType<Separator>(menu.Items[3]);
            Assert.Equal(
                workspace.HasMultipleRecommendations,
                allPngSeparator.IsVisible);
            Assert.Same(allPngAction, menu.Items[4]);
            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                menu.ShowAt(exportButton);
                Dispatcher.UIThread.RunJobs();
                assertExportPngImageIconPresentation(pngAction);
                assertExportRasterLogoPresentation(
                    allPngAction,
                    "ExportAllPngLogoSlot",
                    "ExportAllPngLogoImage",
                    24.0,
                    24.0,
                    null);
                assertExportFluentIconPresentation(
                    appleAction,
                    Icon.CalendarMonth);
                assertExportRasterLogoPresentation(
                    googleAction,
                    "ExportGoogleCalendarLogoSlot",
                    "ExportGoogleCalendarLogoImage",
                    24.0,
                    24.0,
                    0.5);
                menu.Hide();
            }
            Assert.Equal(
                "ExportPngImage",
                AutomationProperties.GetAutomationId(pngAction));
            Assert.Equal(
                "ExportAllPngImages",
                AutomationProperties.GetAutomationId(allPngAction));
            Assert.Equal(
                "가능한 시간표 모두 PNG로 저장",
                allPngAction.Header);
            Assert.Equal(
                "가능한 시간표 모두 PNG로 저장",
                AutomationProperties.GetName(allPngAction));
            Assert.Equal(
                "현재 조건으로 만든 가능한 시간표를 각각 번호가 붙은 PNG 이미지로 저장합니다.",
                AutomationProperties.GetHelpText(allPngAction));
            Assert.Equal(
                "ExportGoogleCalendar",
                AutomationProperties.GetAutomationId(googleAction));
            Assert.Same(workspaceView.ExportPngCommand, pngAction.Command);
            Assert.Same(
                workspaceView.ExportAllPngCommand,
                allPngAction.Command);
            Assert.Same(
                workspaceView.ExportGoogleCalendarCommand,
                googleAction.Command);
            Assert.Equal(
                "현재 시간표를 Google 캘린더로 내보내기",
                AutomationProperties.GetName(googleAction));
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }

    [AvaloniaFact]
    public async Task GoogleCalendarExportUsesTheCurrentPlanAndSeoulTimeAsync()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        RecordingGoogleCalendarExporter googleExporter =
            createSuccessfulGoogleExporter();
        RecordingGoogleCalendarWebNavigator googleCalendarNavigator =
            new RecordingGoogleCalendarWebNavigator(true);
        RecordingAppleCalendarExporter appleExporter =
            createUnavailableAppleExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(
            createServices(
                googleExporter,
                appleExporter,
                googleCalendarNavigator));
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
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        RecordingGoogleCalendarWebNavigator googleCalendarNavigator =
            new RecordingGoogleCalendarWebNavigator(false);
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(
            createServices(
                createSuccessfulGoogleExporter(),
                createUnavailableAppleExporter(),
                googleCalendarNavigator));
        workspaceView.DataContext = workspace;
        Window window = showInWindow(workspaceView);

        try
        {
            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(
                workspaceView.ExportGoogleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            Assert.Equal(1, googleCalendarNavigator.OpenAttemptCount);
            TextBlock status = findRequiredTextBlock(
                workspaceView,
                "ExportStatusText");
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
            PlannerWorkspaceViewModel workspace =
                PlannerWorkspaceTestFactory.CreateWorkspace();
            await workspace.RecommendationRefreshTask;
            RecordingGoogleCalendarWebNavigator googleCalendarNavigator =
                new RecordingGoogleCalendarWebNavigator(true);
            RecordingGoogleCalendarExporter googleExporter =
                new RecordingGoogleCalendarExporter(
                    GoogleCalendarExportResult.Fail(
                        exportStatus,
                        "test_export_failure"));
            ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(
                createServices(
                    googleExporter,
                    createUnavailableAppleExporter(),
                    googleCalendarNavigator));
            workspaceView.DataContext = workspace;
            Window window = showInWindow(workspaceView);

            try
            {
                AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(
                    workspaceView.ExportGoogleCalendarCommand);
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
    public async Task MacExportMenuAndExportUseTheCurrentPlanCalendarAsync()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        RecordingGoogleCalendarExporter googleExporter =
            createSuccessfulGoogleExporter();
        RecordingAppleCalendarExporter appleExporter =
            createSuccessfulAppleExporter();
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView(
            createServices(
                googleExporter,
                appleExporter));
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
            Assert.Equal(5, menu.Items.Count);
            Assert.Equal(
                new[]
                {
                    "PNG 이미지",
                    "Apple 캘린더",
                    "Google 캘린더",
                    "가능한 시간표 모두 PNG로 저장",
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
            MenuItem pngAction = findRequiredMenuItem(
                menu,
                "ExportPngAction");
            MenuItem allPngAction = findRequiredMenuItem(
                menu,
                "ExportAllPngAction");
            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                menu.ShowAt(exportButton);
                Dispatcher.UIThread.RunJobs();
                assertExportPngImageIconPresentation(pngAction);
                assertExportRasterLogoPresentation(
                    allPngAction,
                    "ExportAllPngLogoSlot",
                    "ExportAllPngLogoImage",
                    24.0,
                    24.0,
                    null);
                assertExportFluentIconPresentation(
                    appleAction,
                    Icon.CalendarMonth);
                assertExportRasterLogoPresentation(
                    googleAction,
                    "ExportGoogleCalendarLogoSlot",
                    "ExportGoogleCalendarLogoImage",
                    24.0,
                    24.0,
                    0.5);
                menu.Hide();
            }

            AsyncDelegateCommand command = Assert.IsType<AsyncDelegateCommand>(
                workspaceView.ExportAppleCalendarCommand);
            command.Execute(null);
            await command.ExecutionTask;

            CalendarExportDocument? documentOrNull =
                appleExporter.ExportedDocumentOrNull;
            Assert.NotNull(documentOrNull);
            if (documentOrNull == null)
            {
                throw new InvalidOperationException(
                    "The Apple Calendar export document was not recorded.");
            }

            Assert.Equal(workspace.ActivePlan.PlanId, documentOrNull.PlanId);
            Assert.Equal(workspace.ActivePlan.Name, documentOrNull.CalendarName);
            Assert.Equal("Asia/Seoul", documentOrNull.AcademicCalendar.TimeZoneId.Value);
            Assert.NotEmpty(documentOrNull.Events);
            TextBlock status = findRequiredTextBlock(
                workspaceView,
                "ExportStatusText");
            Assert.Equal("Apple 캘린더로 내보냈습니다.", status.Text);
        }
        finally
        {
            await closeWindowAsync(window, workspaceView);
        }
    }

    private static ScheduleExportServices createServices(
        RecordingGoogleCalendarExporter googleExporter,
        RecordingAppleCalendarExporter appleExporter,
        RecordingGoogleCalendarWebNavigator? googleCalendarNavigatorOrNull = null)
    {
        return new ScheduleExportServices(
            new AvaloniaControlPngExporter(PngExportScale.Create(1.0)),
            googleExporter,
            googleCalendarNavigatorOrNull
                ?? new RecordingGoogleCalendarWebNavigator(true),
            appleExporter,
            new FixedCalendarTimeZoneProvider(
                new CalendarTimeZoneId("Asia/Seoul")));
    }

    private sealed class RecordingGoogleCalendarWebNavigator
        : IGoogleCalendarWebNavigator
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

    private static RecordingAppleCalendarExporter
        createUnavailableAppleExporter()
    {
        return new RecordingAppleCalendarExporter(
            false,
            AppleCalendarExportResult.Fail(
                EAppleCalendarExportStatus.Unavailable,
                "test_unavailable"));
    }

    private static RecordingAppleCalendarExporter createSuccessfulAppleExporter()
    {
        AppleCalendarNativeExportResult nativeResult =
            new AppleCalendarNativeExportResult(
                new AppleCalendarId("test-apple-calendar"),
                new PlanName("2026-2학기 시간표"),
                1,
                0);
        return new RecordingAppleCalendarExporter(
            true,
            AppleCalendarExportResult.Complete(nativeResult));
    }

    private static RecordingGoogleCalendarExporter
        createSuccessfulGoogleExporter()
    {
        GoogleCalendarExportResult result =
            GoogleCalendarExportResult.Complete(
                new GoogleCalendarId("test-calendar@group.calendar.google.com"),
                new PlanName("2026-2학기 시간표"),
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

    private static void assertExportPngImageIconPresentation(
        MenuItem menuItem)
    {
        assertExportRasterLogoPresentation(
            menuItem,
            "ExportPngLogoSlot",
            "ExportPngLogoImage",
            24.0,
            24.0,
            null);
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
        Assert.Equal(
            HorizontalAlignment.Center,
            logoSlot.HorizontalAlignment);
        Assert.Equal(
            VerticalAlignment.Center,
            logoSlot.VerticalAlignment);
        Assert.Contains("export-menu-logo-slot", logoSlot.Classes);

        Image? logoImageOrNull = logoSlot.FindControl<Image>(imageName);
        Assert.NotNull(logoImageOrNull);
        if (logoImageOrNull == null)
        {
            throw new InvalidOperationException(
                "The export menu logo image was not found: " + imageName);
        }

        Assert.NotNull(logoImageOrNull.Source);
        Assert.Equal(imageWidth, logoImageOrNull.Width);
        Assert.Equal(imageHeight, logoImageOrNull.Height);
        Assert.Equal(Stretch.Uniform, logoImageOrNull.Stretch);
        Assert.Equal(
            HorizontalAlignment.Center,
            logoImageOrNull.HorizontalAlignment);
        Assert.Equal(
            VerticalAlignment.Center,
            logoImageOrNull.VerticalAlignment);
        Assert.Contains("export-menu-logo", logoImageOrNull.Classes);
        Assert.Same(logoImageOrNull, Assert.Single(logoSlot.Children));

        if (verticalTranslationOrNull.HasValue)
        {
            TranslateTransform translation = Assert.IsType<TranslateTransform>(
                logoImageOrNull.RenderTransform);
            Assert.Equal(verticalTranslationOrNull.Value, translation.Y);
        }
        else
        {
            Assert.Null(logoImageOrNull.RenderTransform);
        }

        return logoSlot;
    }

    private static void assertExportFluentIconPresentation(
        MenuItem menuItem,
        Icon expectedIcon)
    {
        Assert.Contains("export-menu-item", menuItem.Classes);
        if (menuItem.IsVisible)
        {
            assertExportMenuItemPresentation(menuItem);
        }

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
        Assert.Equal(
            HorizontalAlignment.Center,
            iconPresenter.HorizontalAlignment);
        Assert.Equal(
            VerticalAlignment.Center,
            iconPresenter.VerticalAlignment);
        Assert.Equal(
            HorizontalAlignment.Center,
            iconPresenter.HorizontalContentAlignment);
        Assert.Equal(
            VerticalAlignment.Center,
            iconPresenter.VerticalContentAlignment);

        Point? iconOriginOrNull = iconPresenter.TranslatePoint(
            new Point(0.0, 0.0),
            menuItem);
        Assert.NotNull(iconOriginOrNull);
        if (iconOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The export menu icon geometry could not be resolved.");
        }

        double menuItemCenterY = menuItem.Bounds.Height / 2.0;
        double iconCenterY = iconOriginOrNull.Value.Y
            + (iconPresenter.Bounds.Height / 2.0);
        double iconCenterDelta = iconCenterY - menuItemCenterY;
        Assert.True(
            Math.Abs(iconCenterDelta) <= MAXIMUM_CENTER_DELTA_DIP,
            "Export menu icon center delta=" + iconCenterDelta
                + ", item height=" + menuItem.Bounds.Height
                + ", icon top=" + iconOriginOrNull.Value.Y
                + ", icon height=" + iconPresenter.Bounds.Height + ".");

        string headerText = Assert.IsType<string>(menuItem.Header);
        TextBlock header = menuItem.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(candidate => candidate.Text == headerText);
        Point? headerOriginOrNull = header.TranslatePoint(
            new Point(0.0, 0.0),
            menuItem);
        Assert.NotNull(headerOriginOrNull);
        if (headerOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The export menu header geometry could not be resolved.");
        }

        double headerCenterY = headerOriginOrNull.Value.Y
            + (header.Bounds.Height / 2.0);
        double headerCenterDelta = headerCenterY - menuItemCenterY;
        Assert.True(
            Math.Abs(headerCenterDelta) <= MAXIMUM_CENTER_DELTA_DIP,
            "Export menu header center delta=" + headerCenterDelta
                + ", item height=" + menuItem.Bounds.Height
                + ", header top=" + headerOriginOrNull.Value.Y
                + ", header height=" + header.Bounds.Height + ".");
    }

    private static ThemeVariant[] getProductThemeVariants()
    {
        return new ThemeVariant[]
        {
            ThemeVariant.Light,
            ThemeVariant.Dark,
        };
    }

    private static async Task closeWindowAsync(
        Window window,
        ScheduleWorkspaceView workspaceView)
    {
        window.Close();
        Dispatcher.UIThread.RunJobs();
        await workspaceView.ExportResourceReleaseTask;
        Assert.Null(workspaceView.ExportResourceReleaseExceptionOrNull);
    }
}
