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
        Button primaryAction = createButton("accent");
        Window window = new Window();
        window.Content = primaryAction;

        try
        {
            applicationOrNull.RequestedThemeVariant = ThemeVariant.Dark;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            assertButtonBrush(
                primaryAction.Background,
                "ProductPrimaryActionFillBrush",
                ThemeVariant.Dark);
            assertRenderedButtonBackground(
                primaryAction,
                "ProductPrimaryActionFillBrush",
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
        Button primaryAction = createButton("accent");
        Button dangerAction = createButton("danger");
        Button outlineAction = createButton("outline");
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
                "ProductPrimaryActionFillBrush",
                ThemeVariant.Light);
            assertRenderedButtonBackground(
                primaryAction,
                "ProductPrimaryActionFillBrush",
                ThemeVariant.Light);
            assertButtonBrush(
                primaryAction.Foreground,
                "ProductOnPrimaryActionFillBrush",
                ThemeVariant.Light);
            assertButtonBrush(
                dangerAction.Background,
                "ProductDangerActionFillBrush",
                ThemeVariant.Light);
            assertRenderedButtonBackground(
                dangerAction,
                "ProductDangerActionFillBrush",
                ThemeVariant.Light);
            assertButtonBrush(
                outlineAction.BorderBrush,
                "ControlBorderBrush",
                ThemeVariant.Light);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();

            assertButtonBrush(
                primaryAction.Background,
                "ProductPrimaryActionFillBrush",
                ThemeVariant.Dark);
            assertRenderedButtonBackground(
                primaryAction,
                "ProductPrimaryActionFillBrush",
                ThemeVariant.Dark);
            assertButtonBrush(
                primaryAction.Foreground,
                "ProductOnPrimaryActionFillBrush",
                ThemeVariant.Dark);
            assertButtonBrush(
                dangerAction.Background,
                "ProductDangerActionFillBrush",
                ThemeVariant.Dark);
            assertRenderedButtonBackground(
                dangerAction,
                "ProductDangerActionFillBrush",
                ThemeVariant.Dark);
            assertButtonBrush(
                outlineAction.BorderBrush,
                "ControlBorderBrush",
                ThemeVariant.Dark);

            bool isPrimaryActionFocused = primaryAction.Focus();
            Assert.True(isPrimaryActionFocused);
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(
                primaryAction.Background,
                "ProductPrimaryActionFillBrush",
                ThemeVariant.Dark);
        }
        finally
        {
            window.Close();
        }
    }

    private static Button createButton(string styleClass)
    {
        Button button = new Button();
        button.Classes.Add(styleClass);
        button.Content = styleClass;
        return button;
    }

    private static void assertButtonBrush(
        IBrush? actualBrushOrNull,
        string expectedResourceKey,
        ThemeVariant themeVariant)
    {
        SolidColorBrush actualBrush = Assert.IsType<SolidColorBrush>(
            actualBrushOrNull);
        SolidColorBrush expectedBrush = findRequiredThemeBrush(
            expectedResourceKey,
            themeVariant);
        Assert.Equal(expectedBrush.Color, actualBrush.Color);
    }

    private static void assertRenderedButtonBackground(
        Button button,
        string expectedResourceKey,
        ThemeVariant themeVariant)
    {
        ContentPresenter contentPresenter = button.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .Single();
        assertButtonBrush(
            contentPresenter.Background,
            expectedResourceKey,
            themeVariant);
    }

    private static SolidColorBrush findRequiredThemeBrush(
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
        Assert.True(hasResource, "Missing brush resource: " + resourceKey);
        return Assert.IsType<SolidColorBrush>(resourceOrNull);
    }
}
