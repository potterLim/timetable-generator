using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.ViewModels;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class MainWindow : Window
{
    private readonly PlannerWorkspaceViewModel mWorkspaceViewModel;

    public MainWindow(PlannerWorkspaceViewModel workspaceViewModel)
    {
        ArgumentNullException.ThrowIfNull(workspaceViewModel);

        AvaloniaXamlLoader.Load(this);
        mWorkspaceViewModel = workspaceViewModel;
        DataContext = mWorkspaceViewModel;

        SizeChanged += onSizeChanged;
        KeyDown += onKeyDown;
        mWorkspaceViewModel.applyWorkspaceWidth(new WorkspaceWidth(Width));
    }

    private void onSizeChanged(object? senderOrNull, SizeChangedEventArgs eventArgs)
    {
        mWorkspaceViewModel.applyWorkspaceWidth(new WorkspaceWidth(eventArgs.NewSize.Width));
    }

    private void onKeyDown(object? senderOrNull, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        mWorkspaceViewModel.closeOverlayPanes();
        eventArgs.Handled = true;
    }
}
