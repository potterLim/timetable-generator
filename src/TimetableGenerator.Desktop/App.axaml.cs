using System;
using System.IO;

using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using TimetableGenerator.Desktop.Configuration;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Desktop.Views;

namespace TimetableGenerator.Desktop;

internal sealed class App : Avalonia.Application
{
    private const string CATALOG_CONFIGURATION_FILE_NAME =
        "catalog-source.local.json";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        IClassicDesktopStyleApplicationLifetime? desktopLifetimeOrNull =
            ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktopLifetimeOrNull != null)
        {
            ProductShellViewModel productShell = createProductShell();
            desktopLifetimeOrNull.MainWindow = new MainWindow(productShell);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ProductShellViewModel createProductShell()
    {
        ProductDataPaths dataPaths = new ProductDataPaths(
            ProductDataRootPath.CreateDefault());
        CatalogSourceConfigurationPath configurationPath =
            new CatalogSourceConfigurationPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    CATALOG_CONFIGURATION_FILE_NAME));
        ProductWorkspaceLoader dataLoader = ProductWorkspaceLoader.Create(
            dataPaths,
            configurationPath);
        ProductWorkspaceViewModelLoader viewModelLoader =
            new ProductWorkspaceViewModelLoader(dataLoader);
        return new ProductShellViewModel(viewModelLoader);
    }
}
