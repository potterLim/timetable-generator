using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
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
            TextBlock heading = findRequiredControl<TextBlock>(
                view,
                "AppearanceSettingsHeading");
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
            Assert.Equal(1, (int)AutomationProperties.GetHeadingLevel(heading));
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
            Assert.Equal(
                new Thickness(8.0, 0.0, 0.0, 2.0),
                systemOption.Padding);
            assertIndicatorAndContentUseProductSpacing(systemOption);
            assertIndicatorAndContentUseProductSpacing(lightOption);
            assertIndicatorAndContentUseProductSpacing(darkOption);
            assertIndicatorHasSelectedSurfaceInset(systemOption);
            assertIndicatorHasSelectedSurfaceInset(lightOption);
            assertIndicatorHasSelectedSurfaceInset(darkOption);
            Assert.True(systemOption.IsChecked);
            Assert.False(darkOption.IsChecked);
            assertCheckedOptionUsesSelectedHoverSurface(
                window,
                systemOption);

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
    public void ThemeOptionsUseCompleteVisualStatesAcrossThemes()
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
        window.Width = 320.0;
        window.Height = 280.0;
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
            Border systemSurface = findAppearanceOptionSurface(systemOption);
            Border darkSurface = findAppearanceOptionSurface(darkOption);
            ThemeVariant[] themeVariants =
            {
                ThemeVariant.Light,
                ThemeVariant.Dark,
            };

            foreach (ThemeVariant themeVariant in themeVariants)
            {
                window.RequestedThemeVariant = themeVariant;
                systemOption.IsEnabled = true;
                darkOption.IsEnabled = true;
                systemOption.IsChecked = true;
                movePointerOutsideAppearanceOptions(window);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(true, systemOption.IsChecked);
                Assert.Equal(false, darkOption.IsChecked);
                assertSurfaceUsesResource(
                    systemSurface,
                    "SelectionSurfaceBrush",
                    themeVariant);
                assertTransparentSurface(darkSurface);

                Point darkOptionCenter = findControlCenter(window, darkOption);
                window.MouseMove(darkOptionCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertSurfaceUsesResource(
                    darkSurface,
                    "HoverSurfaceBrush",
                    themeVariant);
                window.MouseDown(
                    darkOptionCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertSurfaceUsesResource(
                    darkSurface,
                    "PressedSurfaceBrush",
                    themeVariant);
                window.MouseUp(
                    darkOptionCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);

                systemOption.IsChecked = true;
                movePointerOutsideAppearanceOptions(window);
                Assert.True(systemOption.Focus(NavigationMethod.Tab));
                Assert.True(darkOption.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                assertTransparentSurface(darkSurface);
                assertFocusVisuals(darkOption, themeVariant);

                darkOption.IsEnabled = false;
                window.MouseMove(darkOptionCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(darkOption.IsEffectivelyEnabled);
                assertTransparentSurface(darkSurface);

                Point systemOptionCenter = findControlCenter(
                    window,
                    systemOption);
                window.MouseMove(systemOptionCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertSurfaceUsesResource(
                    systemSurface,
                    "SelectionHoverSurfaceBrush",
                    themeVariant);
                window.MouseDown(
                    systemOptionCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertSurfaceUsesResource(
                    systemSurface,
                    "SelectionPressedSurfaceBrush",
                    themeVariant);
                window.MouseUp(
                    systemOptionCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);

                movePointerOutsideAppearanceOptions(window);
                darkOption.IsEnabled = true;
                Assert.True(darkOption.Focus(NavigationMethod.Tab));
                Assert.True(systemOption.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                assertSurfaceUsesResource(
                    systemSurface,
                    "SelectionSurfaceBrush",
                    themeVariant);
                assertFocusVisuals(systemOption, themeVariant);

                systemOption.IsEnabled = false;
                window.MouseMove(systemOptionCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(systemOption.IsEffectivelyEnabled);
                assertSurfaceUsesResource(
                    systemSurface,
                    "SelectionSurfaceBrush",
                    themeVariant);
            }
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

    private static void assertIndicatorAndContentUseProductSpacing(
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
        double horizontalGap = presenterOriginOrNull.Value.X
            - indicatorOriginOrNull.Value.X
            - indicator.Bounds.Width;
        Assert.InRange(horizontalGap, 7.75, 8.25);
        double presenterCenterOffset = indicatorCenterY - presenterCenterY;
        Assert.InRange(presenterCenterOffset, 1.25, 1.75);
    }

    private static void assertIndicatorHasSelectedSurfaceInset(
        RadioButton option)
    {
        Visual indicator = option.GetVisualDescendants()
            .Single(candidate => candidate.Name == "OuterEllipse");
        Point? indicatorOriginOrNull = indicator.TranslatePoint(
            new Point(0.0, 0.0),
            option);

        Assert.NotNull(indicatorOriginOrNull);
        if (indicatorOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The appearance option indicator position could not be resolved.");
        }

        Assert.InRange(indicatorOriginOrNull.Value.X, 8.0, 10.0);
    }

    private static void assertCheckedOptionUsesSelectedHoverSurface(
        Window window,
        RadioButton option)
    {
        Border rootBorder = option.GetVisualDescendants()
            .OfType<Border>()
            .Single(candidate => candidate.Name == "RootBorder");
        Color restingColor = getRequiredApplicationColor(
            "SelectionSurfaceBrush",
            option.ActualThemeVariant);
        Assert.Equal(
            restingColor,
            getRequiredSolidColor(rootBorder.Background));
        Point? optionOriginOrNull = option.TranslatePoint(
            new Point(0.0, 0.0),
            window);

        Assert.NotNull(optionOriginOrNull);
        if (optionOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The appearance option position could not be resolved.");
        }

        Point optionCenter = optionOriginOrNull.Value
            + new Vector(option.Bounds.Width / 2.0, option.Bounds.Height / 2.0);
        window.MouseMove(optionCenter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(option.IsPointerOver);
        Assert.Equal(
            getRequiredApplicationColor(
                "SelectionHoverSurfaceBrush",
                option.ActualThemeVariant),
            getRequiredSolidColor(rootBorder.Background));
    }

    private static Border findAppearanceOptionSurface(RadioButton option)
    {
        return option.GetVisualDescendants()
            .OfType<Border>()
            .Single(candidate => candidate.Name == "RootBorder");
    }

    private static Point findControlCenter(Window window, Control control)
    {
        Point? originOrNull = control.TranslatePoint(
            new Point(0.0, 0.0),
            window);
        Assert.NotNull(originOrNull);
        if (originOrNull == null)
        {
            throw new InvalidOperationException(
                "The appearance option position could not be resolved.");
        }

        return originOrNull.Value
            + new Vector(control.Bounds.Width / 2.0, control.Bounds.Height / 2.0);
    }

    private static void movePointerOutsideAppearanceOptions(Window window)
    {
        window.MouseMove(new Point(310.0, 270.0), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static void assertFocusVisuals(
        RadioButton option,
        ThemeVariant themeVariant)
    {
        Assert.Equal(new Thickness(2.0), option.BorderThickness);
        Assert.Equal(
            getRequiredApplicationColor(
                "ProductFocusStrokeBrush",
                themeVariant),
            getRequiredSolidColor(option.BorderBrush));
    }

    private static void assertSurfaceUsesResource(
        Border surface,
        string resourceKey,
        ThemeVariant themeVariant)
    {
        Assert.Equal(
            getRequiredApplicationColor(resourceKey, themeVariant),
            getRequiredSolidColor(surface.Background));
    }

    private static void assertTransparentSurface(Border surface)
    {
        IBrush? backgroundOrNull = surface.Background;
        if (backgroundOrNull == null)
        {
            return;
        }

        Assert.Equal(Colors.Transparent, getRequiredSolidColor(backgroundOrNull));
    }

    private static Color getRequiredApplicationColor(
        string resourceKey,
        ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException(
                "The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(
            resourceKey,
            themeVariant,
            out resourceOrNull);
        Assert.True(hasResource);

        return getRequiredSolidColor(resourceOrNull as IBrush);
    }

    private static Color getRequiredSolidColor(IBrush? brushOrNull)
    {
        ISolidColorBrush? solidBrushOrNull = brushOrNull as ISolidColorBrush;
        Assert.NotNull(solidBrushOrNull);
        if (solidBrushOrNull == null)
        {
            throw new InvalidOperationException(
                "The appearance option surface was not a solid color.");
        }

        return solidBrushOrNull.Color;
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
