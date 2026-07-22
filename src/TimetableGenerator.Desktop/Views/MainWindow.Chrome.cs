using System;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

using FluentIcons.Avalonia;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class MainWindow
{
    private Button? mMaximizeRestoreButtonOrNull;

    private FluentIcon? mMaximizeRestoreIconOrNull;

    private void initializeProductCaptionControls()
    {
        mMaximizeRestoreButtonOrNull = this.FindControl<Button>("WindowMaximizeRestoreButton");
        mMaximizeRestoreIconOrNull = this.FindControl<FluentIcon>("WindowMaximizeRestoreIcon");
        if (mMaximizeRestoreButtonOrNull == null || mMaximizeRestoreIconOrNull == null)
        {
            throw new InvalidOperationException("The product caption controls could not be resolved.");
        }

        PropertyChanged += onWindowChromePropertyChanged;
        synchronizeMaximizeRestoreAction();
    }

    private void disposeProductCaptionControls()
    {
        PropertyChanged -= onWindowChromePropertyChanged;
    }

    private void onWindowChromePropertyChanged(
        object? senderOrNull,
        AvaloniaPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.Property == WindowStateProperty)
        {
            synchronizeMaximizeRestoreAction();
        }
    }

    private void onWindowMinimizeButtonClick(object? senderOrNull, RoutedEventArgs eventArgs)
    {
        WindowState = WindowState.Minimized;
        eventArgs.Handled = true;
    }

    private void onWindowMaximizeRestoreButtonClick(object? senderOrNull, RoutedEventArgs eventArgs)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        eventArgs.Handled = true;
    }

    private void onWindowCloseButtonClick(object? senderOrNull, RoutedEventArgs eventArgs)
    {
        Close();
        eventArgs.Handled = true;
    }

    private void synchronizeMaximizeRestoreAction()
    {
        if (mMaximizeRestoreButtonOrNull == null)
        {
            throw new InvalidOperationException("The maximize or restore button was not initialized.");
        }

        if (mMaximizeRestoreIconOrNull == null)
        {
            throw new InvalidOperationException("The maximize or restore icon was not initialized.");
        }

        Button maximizeRestoreButton = mMaximizeRestoreButtonOrNull;
        FluentIcon maximizeRestoreIcon = mMaximizeRestoreIconOrNull;
        bool isMaximized = WindowState == WindowState.Maximized;
        string actionName = isMaximized ? "복원" : "최대화";

        maximizeRestoreIcon.Icon = isMaximized
            ? FluentIcons.Common.Icon.SquareMultiple
            : FluentIcons.Common.Icon.Square;
        AutomationProperties.SetName(maximizeRestoreButton, actionName);
        ToolTip.SetTip(maximizeRestoreButton, actionName);
    }
}
