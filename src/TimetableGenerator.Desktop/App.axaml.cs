using System;
using System.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using TimetableGenerator.Desktop.Configuration;
using TimetableGenerator.Desktop.Presentation.Appearance;
using TimetableGenerator.Desktop.Presentation.Windowing;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Desktop.Views;

namespace TimetableGenerator.Desktop;

internal sealed class App : Avalonia.Application
{
    private const string CATALOG_CONFIGURATION_FILE_NAME = "catalog-source.local.json";

    private const string TITLE_BAR_CONTENT_PADDING_RESOURCE_KEY = "WindowTitleBarContentPadding";

    private ProductDataPaths? mDataPathsOrNull;

    private IClassicDesktopStyleApplicationLifetime? mDesktopLifetimeOrNull;

    private IActivatableLifetime? mActivatableLifetimeOrNull;

    private AboutWindow? mAboutWindowOrNull;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        EWindowChromePlatform platform = WindowChromeLayoutPolicy.FindCurrentPlatform();
        WindowChromeInsets titleBarInsets = WindowChromeLayoutPolicy.FindTitleBarInsets(platform);
        Resources[TITLE_BAR_CONTENT_PADDING_RESOURCE_KEY] = new Thickness(
            titleBarInsets.Left,
            0.0,
            titleBarInsets.Right,
            0.0);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetimeOrNull = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktopLifetimeOrNull != null)
        {
            ProductDataPaths dataPaths = new ProductDataPaths(ProductDataRootPath.CreateDefault());
            mDataPathsOrNull = dataPaths;
            mDesktopLifetimeOrNull = desktopLifetimeOrNull;
            desktopLifetimeOrNull.MainWindow = createMainWindow(dataPaths);

            if (OperatingSystem.IsMacOS())
            {
                desktopLifetimeOrNull.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                mActivatableLifetimeOrNull = this.TryGetFeature<IActivatableLifetime>();
                if (mActivatableLifetimeOrNull != null)
                {
                    mActivatableLifetimeOrNull.Activated += onApplicationActivated;
                }

                desktopLifetimeOrNull.Exit += onDesktopLifetimeExit;
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private MainWindow createMainWindow(ProductDataPaths dataPaths)
    {
        ProductShellViewModel productShell = createProductShell(dataPaths);
        ProductAppearanceViewModel appearance = createProductAppearance(dataPaths);
        return new MainWindow(productShell, appearance);
    }

    private void onApplicationActivated(object? senderOrNull, ActivatedEventArgs eventArgs)
    {
        if (eventArgs.Kind != ActivationKind.Reopen)
        {
            return;
        }

        IClassicDesktopStyleApplicationLifetime? desktopLifetimeOrNull = mDesktopLifetimeOrNull;
        ProductDataPaths? dataPathsOrNull = mDataPathsOrNull;
        if (desktopLifetimeOrNull == null || dataPathsOrNull == null)
        {
            return;
        }

        Window? mainWindowOrNull = desktopLifetimeOrNull.MainWindow;
        if (mainWindowOrNull == null || isOpenWindow(desktopLifetimeOrNull, mainWindowOrNull) == false)
        {
            mainWindowOrNull = createMainWindow(dataPathsOrNull);
            desktopLifetimeOrNull.MainWindow = mainWindowOrNull;
            mainWindowOrNull.Show();
        }
        else
        {
            if (mainWindowOrNull.WindowState == WindowState.Minimized)
            {
                mainWindowOrNull.WindowState = WindowState.Normal;
            }

            if (mainWindowOrNull.IsVisible == false)
            {
                mainWindowOrNull.Show();
            }
        }

        mainWindowOrNull.Activate();
    }

    private void onDesktopLifetimeExit(
        object? senderOrNull,
        ControlledApplicationLifetimeExitEventArgs eventArgs)
    {
        if (mActivatableLifetimeOrNull != null)
        {
            mActivatableLifetimeOrNull.Activated -= onApplicationActivated;
            mActivatableLifetimeOrNull = null;
        }

        if (mDesktopLifetimeOrNull != null)
        {
            mDesktopLifetimeOrNull.Exit -= onDesktopLifetimeExit;
            mDesktopLifetimeOrNull = null;
        }

        mDataPathsOrNull = null;
    }

    private void onAboutMenuItemClick(object? senderOrNull, EventArgs eventArgs)
    {
        AboutWindow? aboutWindowOrNull = mAboutWindowOrNull;
        if (aboutWindowOrNull != null)
        {
            if (aboutWindowOrNull.WindowState == WindowState.Minimized)
            {
                aboutWindowOrNull.WindowState = WindowState.Normal;
            }

            aboutWindowOrNull.Activate();
            return;
        }

        aboutWindowOrNull = new AboutWindow();
        mAboutWindowOrNull = aboutWindowOrNull;
        aboutWindowOrNull.Closed += onAboutWindowClosed;

        IClassicDesktopStyleApplicationLifetime? desktopLifetimeOrNull = mDesktopLifetimeOrNull;
        Window? mainWindowOrNull = desktopLifetimeOrNull?.MainWindow;
        if (desktopLifetimeOrNull != null
            && mainWindowOrNull != null
            && mainWindowOrNull.IsVisible
            && isOpenWindow(desktopLifetimeOrNull, mainWindowOrNull))
        {
            _ = aboutWindowOrNull.ShowDialog(mainWindowOrNull);
            return;
        }

        aboutWindowOrNull.Show();
    }

    private void onAboutWindowClosed(object? senderOrNull, EventArgs eventArgs)
    {
        if (senderOrNull is AboutWindow aboutWindow)
        {
            aboutWindow.Closed -= onAboutWindowClosed;
            if (ReferenceEquals(mAboutWindowOrNull, aboutWindow))
            {
                mAboutWindowOrNull = null;
            }
        }
    }

    private static bool isOpenWindow(
        IClassicDesktopStyleApplicationLifetime desktopLifetime,
        Window window)
    {
        foreach (Window openWindow in desktopLifetime.Windows)
        {
            if (ReferenceEquals(openWindow, window))
            {
                return true;
            }
        }

        return false;
    }

    private ProductAppearanceViewModel createProductAppearance(ProductDataPaths dataPaths)
    {
        ProductAppearanceSettingsFileStore settingsStore = new ProductAppearanceSettingsFileStore(dataPaths.AppearanceSettings, new ProductAppearanceSettingsJsonCodec());
        AvaloniaProductThemeVariantService themeVariantService = new AvaloniaProductThemeVariantService(this);
        return new ProductAppearanceViewModel(settingsStore, themeVariantService);
    }

    private static ProductShellViewModel createProductShell(ProductDataPaths dataPaths)
    {
        CatalogSourceConfigurationPath configurationPath = new CatalogSourceConfigurationPath(Path.Combine(AppContext.BaseDirectory, CATALOG_CONFIGURATION_FILE_NAME));
        return ProductCompositionRoot.CreateShell(dataPaths, configurationPath);
    }
}
