using System;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TimetableGenerator.Desktop.Presentation.Appearance;
using TimetableGenerator.Desktop.Product.Appearance;
using TimetableGenerator.Desktop.Tests.Presentation.Appearance;
using TimetableGenerator.Desktop.Views;
using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class AppearanceSettingsInteractionTests
{
    [AvaloniaFact]
    public async Task ThemeOptionsReflectAndApplyTheSelectedPreferenceAsync()
    {
        ControlledProductAppearanceSettingsStore settingsStore =
            new ControlledProductAppearanceSettingsStore(
                ProductAppearanceSettings.CreateDefault());
        ProductAppearanceViewModel appearance =
            new ProductAppearanceViewModel(
                settingsStore,
                new RecordingProductThemeVariantService());
        AppearanceSettingsView view = new AppearanceSettingsView();
        view.DataContext = appearance;
        Window window = new Window();
        window.Content = view;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            RadioButton systemOption = findRequiredControl<RadioButton>(
                view,
                "SystemThemeOption");
            RadioButton darkOption = findRequiredControl<RadioButton>(
                view,
                "DarkThemeOption");

            Assert.Equal("ProductThemePreference", systemOption.GroupName);
            Assert.Equal(systemOption.GroupName, darkOption.GroupName);
            Assert.Equal(
                "시스템 모드 사용",
                AutomationProperties.GetName(systemOption));
            Assert.Equal(
                "다크 모드 사용",
                AutomationProperties.GetName(darkOption));
            Assert.True(systemOption.MinHeight >= 44.0);
            Assert.True(darkOption.MinHeight >= 44.0);
            Assert.True(systemOption.IsChecked);
            Assert.False(darkOption.IsChecked);

            darkOption.IsChecked = true;
            await appearance.CompletePersistenceAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.False(systemOption.IsChecked);
            Assert.True(darkOption.IsChecked);
            Assert.Equal(
                EProductThemePreference.Dark,
                settingsStore.SavedSettings[0].ThemePreference);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PersistenceFailureOffersAWorkingRetryActionAsync()
    {
        ControlledProductAppearanceSettingsStore settingsStore =
            new ControlledProductAppearanceSettingsStore(
                ProductAppearanceSettings.CreateDefault());
        settingsStore.FailSaves(
            new ProductAppearanceSettingsException("Controlled failure."));
        ProductAppearanceViewModel appearance =
            new ProductAppearanceViewModel(
                settingsStore,
                new RecordingProductThemeVariantService());
        AppearanceSettingsView view = new AppearanceSettingsView();
        view.DataContext = appearance;
        Window window = new Window();
        window.Content = view;

        try
        {
            window.Show();
            RadioButton darkOption = findRequiredControl<RadioButton>(
                view,
                "DarkThemeOption");
            darkOption.IsChecked = true;
            await appearance.CompletePersistenceAsync();
            Dispatcher.UIThread.RunJobs();
            Button retryButton = findRequiredControl<Button>(
                view,
                "RetryAppearancePersistenceButton");

            Assert.True(appearance.HasPersistenceFailure);
            Assert.True(retryButton.IsEffectivelyVisible);
            Assert.NotNull(retryButton.Command);

            settingsStore.AllowSaves();
            retryButton.Command.Execute(null);
            await appearance.CompletePersistenceAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.False(appearance.HasPersistenceFailure);
            Assert.Equal(
                EProductThemePreference.Dark,
                settingsStore.SavedSettings[0].ThemePreference);
        }
        finally
        {
            window.Close();
        }
    }

    private static TControl findRequiredControl<TControl>(
        Control root,
        string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The appearance control could not be resolved: "
                + controlName);
        }

        return controlOrNull;
    }
}
