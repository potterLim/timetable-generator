using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class WorkspaceHelpView : UserControl
{
    public event EventHandler? DismissRequested;

    public WorkspaceHelpView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void onDismissClicked(
        object? senderOrNull,
        RoutedEventArgs eventArgs)
    {
        DismissRequested?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
    }
}
