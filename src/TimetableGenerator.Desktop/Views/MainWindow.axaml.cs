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
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Appearance;
using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Presentation.Windowing;
using TimetableGenerator.Desktop.Product;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class MainWindow : Window
{
    private static readonly TimeSpan AUTOSAVE_SHUTDOWN_TIMEOUT = TimeSpan.FromSeconds(10.0);

    private static readonly TimeSpan EXPORT_SHUTDOWN_TIMEOUT = TimeSpan.FromSeconds(15.0);

    private static readonly TimeSpan OPERATING_SYSTEM_SHUTDOWN_TIMEOUT = TimeSpan.FromSeconds(2.5);

    private readonly ProductShellViewModel mProductShellViewModel;

    private readonly Task mStartupTask;

    private Task mShutdownTask;

    private CancellationTokenSource? mShutdownModeCancellationSourceOrNull;

    private bool mIsShutdownStarted;

    private bool mIsCloseAuthorized;

    private bool mShouldExitApplicationAfterClose;

    private bool mIsOperatingSystemShutdownRequested;

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
        mShutdownModeCancellationSourceOrNull?.Dispose();
        mShutdownModeCancellationSourceOrNull = null;
        GC.KeepAlive(mStartupTask);
        GC.KeepAlive(mShutdownTask);
    }

    private void onClosing(object? senderOrNull, WindowClosingEventArgs eventArgs)
    {
        if (eventArgs.CloseReason == WindowCloseReason.ApplicationShutdown || eventArgs.CloseReason == WindowCloseReason.OSShutdown)
        {
            mShouldExitApplicationAfterClose = true;
        }

        if (eventArgs.CloseReason == WindowCloseReason.OSShutdown)
        {
            mIsOperatingSystemShutdownRequested = true;
        }

        if (mIsCloseAuthorized)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (mIsOperatingSystemShutdownRequested)
        {
            cancelShutdownModeForOperatingSystemShutdown(mShutdownModeCancellationSourceOrNull);
        }

        if (mIsShutdownStarted)
        {
            return;
        }

        mIsShutdownStarted = true;
        mProductShellViewModel.beginShutdown();
        findWorkspaceHostOrNull()?.blockNewExportsForShutdown();
        mShutdownModeCancellationSourceOrNull = new CancellationTokenSource();
        mShutdownTask = completeShutdownAsync(mShutdownModeCancellationSourceOrNull.Token);
    }

    private async Task completeShutdownAsync(CancellationToken shutdownModeCancellationToken)
    {
        try
        {
            if (mIsOperatingSystemShutdownRequested)
            {
                await completeOperatingSystemShutdownAsync();
            }
            else
            {
                await completeNormalShutdownAsync(shutdownModeCancellationToken);
            }

            authorizeClose();
        }
        catch (OperationCanceledException) when (shutdownModeCancellationToken.IsCancellationRequested && mIsOperatingSystemShutdownRequested)
        {
            await completeOperatingSystemShutdownAsync();
            authorizeClose();
        }
        catch (ExportShutdownTimeoutException exception)
        {
            cancelExportShutdown();
            resetShutdownAttempt();
            mProductShellViewModel.showExportShutdownFailure();
            Trace.TraceWarning("The product window remained open because export shutdown timed out: {0}", exception);
        }
        catch (Exception exception)
        {
            cancelExportShutdown();
            resetShutdownAttempt();
            mProductShellViewModel.showShutdownFailure(exception);
            Trace.TraceError("The product window remained open because graceful shutdown failed: {0}", exception);
        }
    }

    private async Task completeNormalShutdownAsync(CancellationToken shutdownModeCancellationToken)
    {
        using (CancellationTokenSource autosaveTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownModeCancellationToken))
        {
            autosaveTimeoutSource.CancelAfter(AUTOSAVE_SHUTDOWN_TIMEOUT);
            await mProductShellViewModel.PrepareAutosaveForShutdownAsync(autosaveTimeoutSource.Token);
        }

        await Appearance.CompletePersistenceAsync().WaitAsync(shutdownModeCancellationToken);

        ProductWorkspaceHostView? workspaceHostOrNull = findWorkspaceHostOrNull();
        mProductShellViewModel.beginExportShutdown();
        workspaceHostOrNull?.beginExportShutdown();
        using (CancellationTokenSource exportTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownModeCancellationToken))
        {
            exportTimeoutSource.CancelAfter(EXPORT_SHUTDOWN_TIMEOUT);
            try
            {
                if (workspaceHostOrNull != null)
                {
                    await workspaceHostOrNull.completeExportShutdownAsync(exportTimeoutSource.Token);
                }
            }
            catch (OperationCanceledException exception) when (exportTimeoutSource.IsCancellationRequested && shutdownModeCancellationToken.IsCancellationRequested == false)
            {
                throw new ExportShutdownTimeoutException(exception);
            }
        }

        using (CancellationTokenSource autosaveCompletionTimeoutSource = CancellationTokenSource.CreateLinkedTokenSource(shutdownModeCancellationToken))
        {
            autosaveCompletionTimeoutSource.CancelAfter(AUTOSAVE_SHUTDOWN_TIMEOUT);
            await mProductShellViewModel.CompleteAutosaveAsync(autosaveCompletionTimeoutSource.Token);
        }
    }

    private async Task completeOperatingSystemShutdownAsync()
    {
        using (CancellationTokenSource timeoutSource = new CancellationTokenSource(OPERATING_SYSTEM_SHUTDOWN_TIMEOUT))
        {
            try
            {
                ProductWorkspaceHostView? workspaceHostOrNull = findWorkspaceHostOrNull();
                workspaceHostOrNull?.beginExportShutdown();
                await mProductShellViewModel.PrepareAutosaveForShutdownAsync(timeoutSource.Token);
                await Appearance.CompletePersistenceAsync().WaitAsync(timeoutSource.Token);
                if (workspaceHostOrNull != null)
                {
                    await workspaceHostOrNull.completeExportShutdownAsync(timeoutSource.Token);
                }

                await mProductShellViewModel.CompleteAutosaveAsync(timeoutSource.Token);
            }
            catch (Exception exception)
            {
                Trace.TraceWarning("Operating-system shutdown continued after best-effort product cleanup: {0}", exception);
            }
        }
    }

    private ProductWorkspaceHostView? findWorkspaceHostOrNull()
    {
        foreach (Avalonia.Visual visual in this.GetVisualDescendants())
        {
            if (visual is ProductWorkspaceHostView workspaceHost)
            {
                return workspaceHost;
            }
        }

        return null;
    }

    private void cancelExportShutdown()
    {
        findWorkspaceHostOrNull()?.cancelExportShutdown();
    }

    internal static void cancelShutdownModeForOperatingSystemShutdown(CancellationTokenSource? shutdownModeCancellationSourceOrNull)
    {
        try
        {
            shutdownModeCancellationSourceOrNull?.Cancel();
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("Operating-system shutdown continued after the normal shutdown cancellation callback failed: {0}", exception);
        }
    }

    private void resetShutdownAttempt()
    {
        mShutdownModeCancellationSourceOrNull?.Dispose();
        mShutdownModeCancellationSourceOrNull = null;
        mIsShutdownStarted = false;
        mShouldExitApplicationAfterClose = false;
        mIsOperatingSystemShutdownRequested = false;
    }

    private void authorizeClose()
    {
        bool shouldExitApplication = mShouldExitApplicationAfterClose;
        mIsCloseAuthorized = true;
        Close();
        if (shouldExitApplication)
        {
            requestExplicitApplicationShutdown();
        }
    }

    private sealed class ExportShutdownTimeoutException : TimeoutException
    {
        public ExportShutdownTimeoutException(Exception innerException)
            : base("Schedule export shutdown exceeded the product deadline.", innerException)
        {
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
