using System;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ProductControlVisualStateTests
{
    private static readonly ControlStyleClass PRIMARY_ACTION_STYLE =
        new ControlStyleClass("accent");
    private static readonly ControlStyleClass DANGER_ACTION_STYLE =
        new ControlStyleClass("danger");
    private static readonly ControlStyleClass OUTLINE_ACTION_STYLE =
        new ControlStyleClass("outline");

    private static readonly ColorToken PRIMARY_ACTION_FILL =
        new ColorToken("ProductPrimaryActionFillBrush");
    private static readonly ColorToken ON_PRIMARY_ACTION_FILL =
        new ColorToken("ProductOnPrimaryActionFillBrush");
    private static readonly ColorToken DANGER_ACTION_FILL =
        new ColorToken("ProductDangerActionFillBrush");
    private static readonly ColorToken CONTROL_BORDER =
        new ColorToken("ControlBorderBrush");

    [AvaloniaFact]
    public void ProductPrimaryActionUsesFilledBrandColorForApplicationDarkTheme()
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException(
                "The Avalonia test application was not initialized.");
        }

        ThemeVariant? previousThemeVariantOrNull =
            applicationOrNull.RequestedThemeVariant;
        Button primaryAction = createButton(PRIMARY_ACTION_STYLE);
        Window window = new Window();
        window.Content = primaryAction;

        try
        {
            applicationOrNull.RequestedThemeVariant = ThemeVariant.Dark;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            assertButtonBrush(
                primaryAction.Background,
                PRIMARY_ACTION_FILL,
                ThemeVariant.Dark);
            assertRenderedButtonBackground(
                primaryAction,
                PRIMARY_ACTION_FILL,
                ThemeVariant.Dark);
        }
        finally
        {
            window.Close();
            applicationOrNull.RequestedThemeVariant = previousThemeVariantOrNull;
        }
    }

    [AvaloniaFact]
    public void ProductActionStylesResolveProductTokensAcrossThemeChanges()
    {
        Button primaryAction = createButton(PRIMARY_ACTION_STYLE);
        Button dangerAction = createButton(DANGER_ACTION_STYLE);
        Button outlineAction = createButton(OUTLINE_ACTION_STYLE);
        StackPanel actions = new StackPanel();
        actions.Children.Add(primaryAction);
        actions.Children.Add(dangerAction);
        actions.Children.Add(outlineAction);

        Window window = new Window();
        window.RequestedThemeVariant = ThemeVariant.Light;
        window.Content = actions;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            assertButtonBrush(
                primaryAction.Background,
                PRIMARY_ACTION_FILL,
                ThemeVariant.Light);
            assertRenderedButtonBackground(
                primaryAction,
                PRIMARY_ACTION_FILL,
                ThemeVariant.Light);
            assertButtonBrush(
                primaryAction.Foreground,
                ON_PRIMARY_ACTION_FILL,
                ThemeVariant.Light);
            assertButtonBrush(
                dangerAction.Background,
                DANGER_ACTION_FILL,
                ThemeVariant.Light);
            assertRenderedButtonBackground(
                dangerAction,
                DANGER_ACTION_FILL,
                ThemeVariant.Light);
            assertButtonBrush(
                outlineAction.BorderBrush,
                CONTROL_BORDER,
                ThemeVariant.Light);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();

            assertButtonBrush(
                primaryAction.Background,
                PRIMARY_ACTION_FILL,
                ThemeVariant.Dark);
            assertRenderedButtonBackground(
                primaryAction,
                PRIMARY_ACTION_FILL,
                ThemeVariant.Dark);
            assertButtonBrush(
                primaryAction.Foreground,
                ON_PRIMARY_ACTION_FILL,
                ThemeVariant.Dark);
            assertButtonBrush(
                dangerAction.Background,
                DANGER_ACTION_FILL,
                ThemeVariant.Dark);
            assertRenderedButtonBackground(
                dangerAction,
                DANGER_ACTION_FILL,
                ThemeVariant.Dark);
            assertButtonBrush(
                outlineAction.BorderBrush,
                CONTROL_BORDER,
                ThemeVariant.Dark);

            bool isPrimaryActionFocused = primaryAction.Focus();
            Assert.True(isPrimaryActionFocused);
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(
                primaryAction.Background,
                PRIMARY_ACTION_FILL,
                ThemeVariant.Dark);
        }
        finally
        {
            window.Close();
        }
    }

    private static Button createButton(ControlStyleClass styleClass)
    {
        Button button = new Button();
        button.Classes.Add(styleClass.Value);
        button.Content = styleClass.Value;
        return button;
    }

    private static void assertButtonBrush(
        IBrush? actualBrushOrNull,
        ColorToken expectedColorToken,
        ThemeVariant themeVariant)
    {
        SolidColorBrush actualBrush = Assert.IsType<SolidColorBrush>(
            actualBrushOrNull);
        SolidColorBrush expectedBrush = findRequiredThemeBrush(
            expectedColorToken,
            themeVariant);
        Assert.Equal(expectedBrush.Color, actualBrush.Color);
    }

    private static void assertRenderedButtonBackground(
        Button button,
        ColorToken expectedColorToken,
        ThemeVariant themeVariant)
    {
        ContentPresenter contentPresenter = button.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .Single();
        assertButtonBrush(
            contentPresenter.Background,
            expectedColorToken,
            themeVariant);
    }

    private static SolidColorBrush findRequiredThemeBrush(
        ColorToken colorToken,
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
            colorToken.Value,
            themeVariant,
            out resourceOrNull);
        Assert.True(
            hasResource,
            "Missing brush resource: " + colorToken.Value);
        return Assert.IsType<SolidColorBrush>(resourceOrNull);
    }

    private readonly record struct ColorToken(string Value);

    private readonly record struct ControlStyleClass(string Value);
}
