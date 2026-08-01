using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Desktop.Tests.Exporting;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceCalendarExportTests
{
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
        MenuItem? menuItemOrNull = menu.Items.OfType<MenuItem>().SingleOrDefault(menuItem => string.Equals(menuItem.Name, controlName, StringComparison.Ordinal));
        if (menuItemOrNull == null)
        {
            throw new InvalidOperationException("The export menu action was not found: " + controlName);
        }

        return menuItemOrNull;
    }

    private static async Task closeWindowAsync(Window window, ScheduleWorkspaceView workspaceView)
    {
        window.Close();
        Dispatcher.UIThread.RunJobs();
        await workspaceView.ExportResourceReleaseTask;
        Assert.Null(workspaceView.ExportResourceReleaseExceptionOrNull);
    }
}
