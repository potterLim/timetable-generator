using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Presentation.Windowing;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class CalendarNameConflictDialog : Window
{
    private ECalendarNameConflictResolution mResolution;

    public bool UsesProductCaptionControls { get; }

    public CalendarNameConflictDialog()
    {
        EWindowChromePlatform windowChromePlatform =
            WindowChromeLayoutPolicy.FindCurrentPlatform();
        WindowDecorations = WindowChromeLayoutPolicy.FindWindowDecorations(
            windowChromePlatform);
        UsesProductCaptionControls =
            WindowDecorations == Avalonia.Controls.WindowDecorations.None;
        ExtendClientAreaToDecorationsHint = UsesProductCaptionControls;
        ExtendClientAreaTitleBarHeightHint = UsesProductCaptionControls
            ? 42.0
            : -1.0;
        AvaloniaXamlLoader.Load(this);
        KeyDown += onKeyDown;
        Opened += onOpened;
        Closed += onClosed;
    }

    public CalendarNameConflictDialog(CalendarNameConflict conflict)
        : this()
    {
        if (conflict == null)
        {
            throw new ArgumentNullException(nameof(conflict));
        }

        TextBlock availableNameDescription = findRequiredControl<TextBlock>(
            "AvailableNameDescription");
        TextBlock replacementUnavailableDescription =
            findRequiredControl<TextBlock>(
                "ReplacementUnavailableDescription");
        Button replaceButton = findRequiredControl<Button>("ReplaceButton");
        availableNameDescription.Text = "새로 만들면 \""
            + conflict.NextAvailableName.Value
            + "\"로 저장됩니다.";
        replaceButton.IsEnabled = conflict.CanReplace;
        replacementUnavailableDescription.IsVisible = conflict.CanReplace == false;

        string providerName = conflict.Provider switch
        {
            ECalendarExportProvider.Google => "Google 캘린더",
            ECalendarExportProvider.Apple => "Apple 캘린더",
            ECalendarExportProvider.None => throw new ArgumentOutOfRangeException(
                nameof(conflict),
                conflict.Provider,
                "A calendar conflict requires an export provider."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(conflict),
                conflict.Provider,
                "Unknown calendar export provider."),
        };
        Avalonia.Automation.AutomationProperties.SetName(
            this,
            providerName + "의 같은 이름 캘린더 확인");
    }

    private void onWindowCloseButtonClick(
        object? senderOrNull,
        Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        closeWithResolution(ECalendarNameConflictResolution.Cancel);
        eventArgs.Handled = true;
    }

    private void onReplaceButtonClick(
        object? senderOrNull,
        Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        closeWithResolution(
            ECalendarNameConflictResolution.ReplaceExisting);
    }

    private void onCreateButtonClick(
        object? senderOrNull,
        Avalonia.Interactivity.RoutedEventArgs eventArgs)
    {
        closeWithResolution(
            ECalendarNameConflictResolution.CreateWithAvailableName);
    }

    private void onKeyDown(object? senderOrNull, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        eventArgs.Handled = true;
        closeWithResolution(ECalendarNameConflictResolution.Cancel);
    }

    private void onOpened(object? senderOrNull, EventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(
            delegate
            {
                findRequiredControl<Button>("CreateButton").Focus();
            },
            DispatcherPriority.Input);
    }

    private void onClosed(object? senderOrNull, EventArgs eventArgs)
    {
        KeyDown -= onKeyDown;
        Opened -= onOpened;
        Closed -= onClosed;
    }

    private void closeWithResolution(
        ECalendarNameConflictResolution resolution)
    {
        if (mResolution != ECalendarNameConflictResolution.None)
        {
            return;
        }

        mResolution = resolution;
        Close(resolution);
    }

    private TControl findRequiredControl<TControl>(string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = this.FindControl<TControl>(controlName);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The calendar conflict dialog control is unavailable: "
                    + controlName);
        }

        return controlOrNull;
    }
}
