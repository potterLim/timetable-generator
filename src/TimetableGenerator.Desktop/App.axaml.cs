using System;
using System.IO;

using Avalonia;
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
    private const string CATALOG_CONFIGURATION_FILE_NAME =
        "catalog-source.local.json";

    private const string TITLE_BAR_CONTENT_PADDING_RESOURCE_KEY =
        "WindowTitleBarContentPadding";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        EWindowChromePlatform platform =
            WindowChromeLayoutPolicy.FindCurrentPlatform();
        WindowChromeInsets titleBarInsets =
            WindowChromeLayoutPolicy.FindTitleBarInsets(platform);
        Resources[TITLE_BAR_CONTENT_PADDING_RESOURCE_KEY] = new Thickness(
            titleBarInsets.Left,
            0.0,
            titleBarInsets.Right,
            0.0);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetimeOrNull =
            ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktopLifetimeOrNull != null)
        {
            ProductDataPaths dataPaths = new ProductDataPaths(
                ProductDataRootPath.CreateDefault());
            ProductShellViewModel productShell = createProductShell(dataPaths);
            ProductAppearanceViewModel appearance =
                createProductAppearance(dataPaths);
            desktopLifetimeOrNull.MainWindow = new MainWindow(
                productShell,
                appearance);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private ProductAppearanceViewModel createProductAppearance(
        ProductDataPaths dataPaths)
    {
        ProductAppearanceSettingsFileStore settingsStore =
            new ProductAppearanceSettingsFileStore(
                dataPaths.AppearanceSettings,
                new ProductAppearanceSettingsJsonCodec());
        AvaloniaProductThemeVariantService themeVariantService =
            new AvaloniaProductThemeVariantService(this);
        return new ProductAppearanceViewModel(
            settingsStore,
            themeVariantService);
    }

    private static ProductShellViewModel createProductShell(
        ProductDataPaths dataPaths)
    {
        CatalogSourceConfigurationPath configurationPath =
            new CatalogSourceConfigurationPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    CATALOG_CONFIGURATION_FILE_NAME));
        return ProductCompositionRoot.CreateShell(
            dataPaths,
            configurationPath);
    }
}
