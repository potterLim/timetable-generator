using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

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
            RadioButton lightOption = findRequiredControl<RadioButton>(
                view,
                "LightThemeOption");
            RadioButton darkOption = findRequiredControl<RadioButton>(
                view,
                "DarkThemeOption");

            Assert.Equal("ProductThemePreference", systemOption.GroupName);
            Assert.Equal(systemOption.GroupName, darkOption.GroupName);
            Assert.Equal(
                "시스템 설정에 맞춰 화면 모드 사용",
                AutomationProperties.GetName(systemOption));
            Assert.Equal(
                "다크 모드 사용",
                AutomationProperties.GetName(darkOption));
            Assert.True(systemOption.MinHeight >= 44.0);
            Assert.True(darkOption.MinHeight >= 44.0);
            Assert.Equal("시스템 설정 사용", systemOption.Content);
            Assert.Equal(
                Avalonia.Layout.VerticalAlignment.Center,
                systemOption.VerticalContentAlignment);
            assertIndicatorAndContentAreVerticallyAligned(systemOption);
            assertIndicatorAndContentAreVerticallyAligned(lightOption);
            assertIndicatorAndContentAreVerticallyAligned(darkOption);
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

    private static void assertIndicatorAndContentAreVerticallyAligned(
        RadioButton option)
    {
        Visual indicator = option.GetVisualDescendants()
            .Single(candidate => candidate.Name == "OuterEllipse");
        ContentPresenter presenter = option.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .Single(candidate => candidate.Name == "PART_ContentPresenter");
        Point? indicatorOriginOrNull = indicator.TranslatePoint(
            new Point(0.0, 0.0),
            option);
        Point? presenterOriginOrNull = presenter.TranslatePoint(
            new Point(0.0, 0.0),
            option);

        Assert.NotNull(indicatorOriginOrNull);
        Assert.NotNull(presenterOriginOrNull);
        if (indicatorOriginOrNull == null || presenterOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The appearance option geometry could not be resolved.");
        }

        double indicatorCenterY = indicatorOriginOrNull.Value.Y
            + (indicator.Bounds.Height / 2.0);
        double presenterCenterY = presenterOriginOrNull.Value.Y
            + (presenter.Bounds.Height / 2.0);
        Assert.InRange(
            Math.Abs(indicatorCenterY - presenterCenterY),
            0.0,
            1.0);
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
