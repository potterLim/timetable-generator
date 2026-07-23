using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class AboutWindow : Window
{
    public AboutWindow()
    {
        AvaloniaXamlLoader.Load(this);
        TextBlock productVersionText = findRequiredControl<TextBlock>("ProductVersionText");
        Version? productVersionOrNull = typeof(AboutWindow).Assembly.GetName().Version;
        string productVersion = "1.0.0";
        if (productVersionOrNull != null)
        {
            productVersion = productVersionOrNull.ToString(3);
        }

        productVersionText.Text = "Version " + productVersion;
        KeyDown += onKeyDown;
        Closed += onClosed;
    }

    private void onCloseButtonClick(object? senderOrNull, RoutedEventArgs eventArgs)
    {
        Close();
        eventArgs.Handled = true;
    }

    private void onKeyDown(object? senderOrNull, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        eventArgs.Handled = true;
        Close();
    }

    private void onClosed(object? senderOrNull, EventArgs eventArgs)
    {
        KeyDown -= onKeyDown;
        Closed -= onClosed;
    }

    private TControl findRequiredControl<TControl>(string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = this.FindControl<TControl>(controlName);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The About window control is unavailable: "
                    + controlName);
        }

        return controlOrNull;
    }
}
