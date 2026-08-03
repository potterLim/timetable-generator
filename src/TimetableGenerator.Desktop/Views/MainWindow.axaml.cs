using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation.Appearance;
using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Presentation.Windowing;
using TimetableGenerator.Desktop.Product;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class MainWindow : Window
{
    private readonly ProductShellViewModel mProductShellViewModel;

    private readonly Task mStartupTask;

    private Task mShutdownTask;

    private bool mIsShutdownStarted;

    private bool mIsCloseAuthorized;

    private bool mShouldExitApplicationAfterClose;

    public ProductAppearanceViewModel Appearance { get; }

    public bool ShouldUseProductCaptionControls { get; }

    public MainWindow(ProductShellViewModel productShellViewModel, ProductAppearanceViewModel appearance)
    {
        ArgumentNullException.ThrowIfNull(productShellViewModel);
        ArgumentNullException.ThrowIfNull(appearance);

        mProductShellViewModel = productShellViewModel;
        Appearance = appearance;
        EWindowChromePlatform windowChromePlatform = WindowChromeLayoutPolicy.FindCurrentPlatform();
        WindowDecorations = WindowChromeLayoutPolicy.FindWindowDecorations(windowChromePlatform);
        ShouldUseProductCaptionControls = WindowDecorations == Avalonia.Controls.WindowDecorations.None;
        AvaloniaXamlLoader.Load(this);
        DataContext = mProductShellViewModel;
        initializeProductCaptionControls();
        initializeNativeMenu();
        initializeWorkspaceInteraction();
        applyInitialWindowPlacement();

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

        PlannerWorkspaceViewModel? workspaceOrNull = mProductShellViewModel.WorkspaceOrNull;
        if (workspaceOrNull == null)
        {
            return;
        }

        if (workspaceOrNull.tryCloseTopmostTransientWorkspaceOverlay())
        {
            eventArgs.Handled = true;
        }
    }

    private void onClosed(object? senderOrNull, EventArgs eventArgs)
    {
        SizeChanged -= onSizeChanged;
        KeyDown -= onKeyDown;
        Closing -= onClosing;
        Closed -= onClosed;
        mProductShellViewModel.PropertyChanged -= onProductShellPropertyChanged;
        disposeWorkspaceInteraction();
        disposeNativeMenu();
        disposeProductCaptionControls();
        mProductShellViewModel.Dispose();
        GC.KeepAlive(mStartupTask);
        GC.KeepAlive(mShutdownTask);
    }

    private void onClosing(object? senderOrNull, WindowClosingEventArgs eventArgs)
    {
        if (eventArgs.CloseReason == WindowCloseReason.ApplicationShutdown || eventArgs.CloseReason == WindowCloseReason.OSShutdown)
        {
            mShouldExitApplicationAfterClose = true;
        }

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
            using (CancellationTokenSource timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(10.0)))
            {
                await mProductShellViewModel.CompleteAutosaveAsync(timeoutSource.Token);
            }

            await Appearance.CompletePersistenceAsync();

            bool shouldExitApplication = mShouldExitApplicationAfterClose;
            mIsCloseAuthorized = true;
            Close();
            if (shouldExitApplication)
            {
                requestExplicitApplicationShutdown();
            }
        }
        catch (Exception exception)
        {
            mIsShutdownStarted = false;
            mShouldExitApplicationAfterClose = false;
            mProductShellViewModel.showShutdownFailure(exception);
            Trace.TraceError("The product window remained open because autosave completion failed: {0}", exception);
        }
    }

    private static void requestExplicitApplicationShutdown()
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetimeOrNull = Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktopLifetimeOrNull == null || desktopLifetimeOrNull.ShutdownMode != ShutdownMode.OnExplicitShutdown)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            delegate
            {
                desktopLifetimeOrNull.TryShutdown();
            });
    }

    private void onProductShellPropertyChanged(object? senderOrNull, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ProductShellViewModel.WorkspaceOrNull))
        {
            connectWorkspaceInteraction();
            applyWorkspaceWidth(Bounds.Width);
        }
        else if (eventArgs.PropertyName == nameof(ProductShellViewModel.IsProductInteractionEnabled))
        {
            updateAppearanceInteraction();
        }
        else if (eventArgs.PropertyName == nameof(ProductShellViewModel.HasShutdownError) && mProductShellViewModel.HasShutdownError)
        {
            Dispatcher.UIThread.Post(focusContinueEditingButton, DispatcherPriority.Input);
        }
    }

    private void focusContinueEditingButton()
    {
        Button? continueEditingButtonOrNull = this.FindControl<Button>("ContinueEditingButton");
        continueEditingButtonOrNull?.Focus();
    }

    private void applyWorkspaceWidth(double width)
    {
        PlannerWorkspaceViewModel? workspaceOrNull = mProductShellViewModel.WorkspaceOrNull;
        if (workspaceOrNull != null && width > 0.0)
        {
            workspaceOrNull.applyWorkspaceWidth(new WorkspaceWidth(width));
        }
    }

    private void applyInitialWindowPlacement()
    {
        Screen? primaryScreenOrNull = Screens.Primary;
        if (primaryScreenOrNull == null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        DisplayScale displayScale = new DisplayScale(primaryScreenOrNull.Scaling);
        WindowWorkingArea workingArea = new WindowWorkingArea(primaryScreenOrNull.WorkingArea, displayScale);
        InitialWindowPlacement placement = InitialWindowPlacementPolicy.CreatePlacement(workingArea);

        MinWidth = placement.EffectiveMinimumSize.Width;
        MinHeight = placement.EffectiveMinimumSize.Height;
        Width = placement.InitialSize.Width;
        Height = placement.InitialSize.Height;
        Position = placement.Position;
        WindowStartupLocation = WindowStartupLocation.Manual;
    }
}
