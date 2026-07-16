using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class MainWindow : Window
{
    private readonly ProductShellViewModel mProductShellViewModel;

    private readonly Task mStartupTask;

    private Task mShutdownTask;

    private bool mIsShutdownStarted;

    private bool mIsCloseAuthorized;

    public MainWindow(ProductShellViewModel productShellViewModel)
    {
        ArgumentNullException.ThrowIfNull(productShellViewModel);

        AvaloniaXamlLoader.Load(this);
        mProductShellViewModel = productShellViewModel;
        DataContext = mProductShellViewModel;

        SizeChanged += onSizeChanged;
        KeyDown += onKeyDown;
        Closing += onClosing;
        Closed += onClosed;
        mProductShellViewModel.PropertyChanged += onProductShellPropertyChanged;
        mStartupTask = mProductShellViewModel.StartAsync();
        mShutdownTask = Task.CompletedTask;
    }

    private void onSizeChanged(object? senderOrNull, SizeChangedEventArgs eventArgs)
    {
        applyWorkspaceWidth(eventArgs.NewSize.Width);
    }

    private void onKeyDown(object? senderOrNull, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        PlannerWorkspaceViewModel? workspaceOrNull =
            mProductShellViewModel.WorkspaceOrNull;
        if (workspaceOrNull == null)
        {
            return;
        }

        workspaceOrNull.closeOverlayPanes();
        eventArgs.Handled = true;
    }

    private void onClosed(object? senderOrNull, EventArgs eventArgs)
    {
        SizeChanged -= onSizeChanged;
        KeyDown -= onKeyDown;
        Closing -= onClosing;
        Closed -= onClosed;
        mProductShellViewModel.PropertyChanged -= onProductShellPropertyChanged;
        mProductShellViewModel.Dispose();
        GC.KeepAlive(mStartupTask);
        GC.KeepAlive(mShutdownTask);
    }

    private void onClosing(object? senderOrNull, WindowClosingEventArgs eventArgs)
    {
        if (mIsCloseAuthorized)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (mIsShutdownStarted)
        {
            return;
        }

        mIsShutdownStarted = true;
        mProductShellViewModel.beginShutdown();
        mShutdownTask = completeShutdownAsync();
    }

    private async Task completeShutdownAsync()
    {
        try
        {
            using (CancellationTokenSource timeoutSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(10.0)))
            {
                await mProductShellViewModel.CompleteAutosaveAsync(
                    timeoutSource.Token);
            }

            mIsCloseAuthorized = true;
            Close();
        }
        catch (Exception exception)
        {
            mIsShutdownStarted = false;
            mProductShellViewModel.showShutdownFailure(exception);
            Trace.TraceError(
                "The product window remained open because autosave completion failed: {0}",
                exception);
        }
    }

    private void onProductShellPropertyChanged(
        object? senderOrNull,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ProductShellViewModel.WorkspaceOrNull))
        {
            applyWorkspaceWidth(Bounds.Width);
        }
    }

    private void applyWorkspaceWidth(double width)
    {
        PlannerWorkspaceViewModel? workspaceOrNull =
            mProductShellViewModel.WorkspaceOrNull;
        if (workspaceOrNull != null && width > 0.0)
        {
            workspaceOrNull.applyWorkspaceWidth(new WorkspaceWidth(width));
        }
    }
}
