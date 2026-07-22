using System;
using System.Threading.Tasks;

using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using TimetableGenerator.Desktop.Presentation.Appearance;
using TimetableGenerator.Desktop.Product.Appearance;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Presentation.Appearance;

public sealed class ProductAppearanceViewModelTests
{
    [Fact]
    public void LoadedPreferenceIsAppliedBeforeInteraction()
    {
        ControlledProductAppearanceSettingsStore settingsStore = new ControlledProductAppearanceSettingsStore(new ProductAppearanceSettings(EProductThemePreference.Dark));
        RecordingProductThemeVariantService themeVariantService = new RecordingProductThemeVariantService();

        ProductAppearanceViewModel viewModel = new ProductAppearanceViewModel(
            settingsStore,
            themeVariantService);

        Assert.Equal(EProductThemePreference.Dark, viewModel.ThemePreference);
        Assert.True(viewModel.IsDarkThemeSelected);
        Assert.False(viewModel.IsLightThemeSelected);
        Assert.False(viewModel.IsSystemThemeSelected);
        Assert.Equal(
            new EProductThemePreference[] { EProductThemePreference.Dark },
            themeVariantService.AppliedPreferences);
    }

    [AvaloniaFact]
    public async Task SelectionAppliesImmediatelyAndPersistsOffTheUiThreadAsync()
    {
        ControlledProductAppearanceSettingsStore settingsStore = new ControlledProductAppearanceSettingsStore(ProductAppearanceSettings.CreateDefault());
        RecordingProductThemeVariantService themeVariantService = new RecordingProductThemeVariantService();
        ProductAppearanceViewModel viewModel = new ProductAppearanceViewModel(
            settingsStore,
            themeVariantService);

        int inputThreadId = Environment.CurrentManagedThreadId;

        viewModel.IsLightThemeSelected = true;
        Assert.Equal(EProductThemePreference.Light, viewModel.ThemePreference);
        Assert.True(viewModel.IsLightThemeSelected);
        await viewModel.CompletePersistenceAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(EProductThemePreference.Light, settingsStore.SavedSettings[0].ThemePreference);
        Assert.NotEqual(inputThreadId, settingsStore.SaveThreadIds[0]);
        Assert.Equal(
            new EProductThemePreference[]
            {
                EProductThemePreference.System,
                EProductThemePreference.Light,
            },
            themeVariantService.AppliedPreferences);
    }

    [AvaloniaFact]
    public async Task SaveFailureKeepsAppliedSelectionAndCanBeRetriedAsync()
    {
        ControlledProductAppearanceSettingsStore settingsStore = new ControlledProductAppearanceSettingsStore(ProductAppearanceSettings.CreateDefault());
        settingsStore.FailSaves(new ProductAppearanceSettingsException("Controlled failure."));
        RecordingProductThemeVariantService themeVariantService = new RecordingProductThemeVariantService();
        ProductAppearanceViewModel viewModel = new ProductAppearanceViewModel(
            settingsStore,
            themeVariantService);

        viewModel.IsDarkThemeSelected = true;
        await viewModel.CompletePersistenceAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(EProductThemePreference.Dark, viewModel.ThemePreference);
        Assert.True(viewModel.HasPersistenceFailure);
        Assert.NotEmpty(viewModel.PersistenceFailureMessage);

        settingsStore.AllowSaves();
        viewModel.RetryPersistenceCommand.Execute(null);
        await viewModel.CompletePersistenceAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.False(viewModel.HasPersistenceFailure);
        Assert.Equal(EProductThemePreference.Dark, settingsStore.SavedSettings[0].ThemePreference);
        Assert.Equal(2, themeVariantService.AppliedPreferences.Count);
    }
}
