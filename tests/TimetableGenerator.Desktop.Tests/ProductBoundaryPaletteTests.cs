using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ProductBoundaryPaletteTests
{
    [AvaloniaFact]
    public void LightBoundariesAndInteractionStatesUseRestrainedCoolBlues()
    {
        assertPalette(
            ThemeVariant.Light,
            new ExpectedColor("ControlHoverSurfaceBrush", "#EEF5FD"),
            new ExpectedColor("SubtleSurfaceBrush", "#EEF4FA"),
            new ExpectedColor("HoverSurfaceBrush", "#E8F1FB"),
            new ExpectedColor("PressedSurfaceBrush", "#DCE9F7"),
            new ExpectedColor("BorderBrush", "#D7E3F0"),
            new ExpectedColor("PaneDividerBrush", "#CBD9E8"),
            new ExpectedColor("StrongBorderBrush", "#B7C9DC"),
            new ExpectedColor("ControlBorderBrush", "#72859E"),
            new ExpectedColor("AccentTintBrush", "#E5F2FF"));
    }

    [AvaloniaFact]
    public void DarkBoundariesAndInteractionStatesUseRestrainedCoolBlues()
    {
        assertPalette(
            ThemeVariant.Dark,
            new ExpectedColor("ControlHoverSurfaceBrush", "#202B37"),
            new ExpectedColor("SubtleSurfaceBrush", "#222F3B"),
            new ExpectedColor("HoverSurfaceBrush", "#293B4D"),
            new ExpectedColor("PressedSurfaceBrush", "#334A60"),
            new ExpectedColor("BorderBrush", "#303F50"),
            new ExpectedColor("PaneDividerBrush", "#394B5D"),
            new ExpectedColor("StrongBorderBrush", "#465D72"),
            new ExpectedColor("ControlBorderBrush", "#71869F"),
            new ExpectedColor("AccentTintBrush", "#172F4A"));
    }

    private static void assertPalette(
        ThemeVariant themeVariant,
        params ExpectedColor[] expectedColors)
    {
        foreach (ExpectedColor expectedColor in expectedColors)
        {
            SolidColorBrush brush = findRequiredBrush(
                expectedColor.Token,
                themeVariant);
            Assert.Equal(Color.Parse(expectedColor.HexColor), brush.Color);
        }
    }

    private static SolidColorBrush findRequiredBrush(
        ColorToken colorToken,
        ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new System.InvalidOperationException(
                "The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(
            colorToken.Value,
            themeVariant,
            out resourceOrNull);
        Assert.True(
            hasResource,
            "The product color token could not be resolved: " +
                colorToken.Value);

        SolidColorBrush? brushOrNull = resourceOrNull as SolidColorBrush;
        Assert.NotNull(brushOrNull);
        if (brushOrNull == null)
        {
            throw new System.InvalidOperationException(
                "The product color token was not a solid color brush: " +
                    colorToken.Value);
        }

        return brushOrNull;
    }

    private readonly record struct ColorToken(string Value);

    private readonly record struct ExpectedColor(
        ColorToken Token,
        string HexColor)
    {
        public ExpectedColor(string token, string hexColor)
            : this(new ColorToken(token), hexColor)
        {
        }
    }
}
