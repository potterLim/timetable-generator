using System;
using System.Collections.Generic;

using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ProductColorContrastTests
{
    private const double RED_LUMINANCE_WEIGHT = 0.2126;
    private const double GREEN_LUMINANCE_WEIGHT = 0.7152;
    private const double BLUE_LUMINANCE_WEIGHT = 0.0722;
    private const double CONTRAST_LUMINANCE_OFFSET = 0.05;
    private const double SRGB_LINEAR_THRESHOLD = 0.04045;
    private const double SRGB_LINEAR_DIVISOR = 12.92;
    private const double SRGB_CURVE_OFFSET = 0.055;
    private const double SRGB_CURVE_DIVISOR = 1.055;
    private const double SRGB_CURVE_EXPONENT = 2.4;
    private const double COLOR_CHANNEL_MAXIMUM = byte.MaxValue;

    private static readonly ColorToken ACCENT_FILL =
        new ColorToken("AccentFillBrush");
    private static readonly ColorToken ERROR_FILL =
        new ColorToken("ErrorFillBrush");
    private static readonly ColorToken FOCUS_STROKE =
        new ColorToken("FocusStrokeBrush");
    private static readonly ColorToken ON_ACCENT_FILL =
        new ColorToken("OnAccentFillBrush");
    private static readonly ColorToken ON_ERROR_FILL =
        new ColorToken("OnErrorFillBrush");
    private static readonly ColorToken SURFACE =
        new ColorToken("SurfaceBrush");
    private static readonly ColorToken TEXT_TERTIARY =
        new ColorToken("TextTertiaryBrush");
    private static readonly ColorToken WINDOW_BACKGROUND =
        new ColorToken("WindowBackgroundBrush");

    private static readonly ContrastRatio MINIMUM_BODY_TEXT_CONTRAST =
        new ContrastRatio(4.5);
    private static readonly ContrastRatio MINIMUM_NON_TEXT_CONTRAST =
        new ContrastRatio(3.0);

    [AvaloniaFact]
    public void ActionFillTokensMeetTextContrastInLightAndDarkThemes()
    {
        ContrastRequirement[] contrastRequirements =
        {
            new ContrastRequirement(
                ThemeVariant.Light,
                ON_ACCENT_FILL,
                ACCENT_FILL,
                MINIMUM_BODY_TEXT_CONTRAST),
            new ContrastRequirement(
                ThemeVariant.Dark,
                ON_ACCENT_FILL,
                ACCENT_FILL,
                MINIMUM_BODY_TEXT_CONTRAST),
            new ContrastRequirement(
                ThemeVariant.Light,
                ON_ERROR_FILL,
                ERROR_FILL,
                MINIMUM_BODY_TEXT_CONTRAST),
            new ContrastRequirement(
                ThemeVariant.Dark,
                ON_ERROR_FILL,
                ERROR_FILL,
                MINIMUM_BODY_TEXT_CONTRAST),
        };

        assertContrastRequirements(contrastRequirements);
    }

    [AvaloniaFact]
    public void LightTertiaryTextMeetsTextContrastOnPrimarySurfaces()
    {
        ContrastRequirement[] contrastRequirements =
        {
            new ContrastRequirement(
                ThemeVariant.Light,
                TEXT_TERTIARY,
                WINDOW_BACKGROUND,
                MINIMUM_BODY_TEXT_CONTRAST),
            new ContrastRequirement(
                ThemeVariant.Light,
                TEXT_TERTIARY,
                SURFACE,
                MINIMUM_BODY_TEXT_CONTRAST),
        };

        assertContrastRequirements(contrastRequirements);
    }

    [AvaloniaFact]
    public void FocusStrokeMeetsNonTextContrastOnLightAndDarkSurfaces()
    {
        ContrastRequirement[] contrastRequirements =
        {
            new ContrastRequirement(
                ThemeVariant.Light,
                FOCUS_STROKE,
                WINDOW_BACKGROUND,
                MINIMUM_NON_TEXT_CONTRAST),
            new ContrastRequirement(
                ThemeVariant.Light,
                FOCUS_STROKE,
                SURFACE,
                MINIMUM_NON_TEXT_CONTRAST),
            new ContrastRequirement(
                ThemeVariant.Dark,
                FOCUS_STROKE,
                WINDOW_BACKGROUND,
                MINIMUM_NON_TEXT_CONTRAST),
            new ContrastRequirement(
                ThemeVariant.Dark,
                FOCUS_STROKE,
                SURFACE,
                MINIMUM_NON_TEXT_CONTRAST),
        };

        assertContrastRequirements(contrastRequirements);
    }

    private static void assertContrastRequirements(
        IReadOnlyList<ContrastRequirement> contrastRequirements)
    {
        foreach (ContrastRequirement contrastRequirement in contrastRequirements)
        {
            assertContrastRequirement(contrastRequirement);
        }
    }

    private static void assertContrastRequirement(
        ContrastRequirement contrastRequirement)
    {
        SolidColorBrush foregroundBrush = findRequiredBrush(
            contrastRequirement.Foreground,
            contrastRequirement.ThemeVariant);
        SolidColorBrush backgroundBrush = findRequiredBrush(
            contrastRequirement.Background,
            contrastRequirement.ThemeVariant);

        Assert.Equal(byte.MaxValue, foregroundBrush.Color.A);
        Assert.Equal(byte.MaxValue, backgroundBrush.Color.A);

        ContrastRatio actualContrast = calculateContrastRatio(
            foregroundBrush.Color,
            backgroundBrush.Color);
        string failureMessage =
            contrastRequirement.ThemeVariant + " theme " +
            contrastRequirement.Foreground.Value + " on " +
            contrastRequirement.Background.Value + " has contrast " +
            actualContrast.Value.ToString("F2") + ":1; required " +
            contrastRequirement.Minimum.Value.ToString("F2") + ":1 or greater.";

        Assert.True(
            actualContrast.IsAtLeast(contrastRequirement.Minimum),
            failureMessage);
    }

    private static SolidColorBrush findRequiredBrush(
        ColorToken colorToken,
        ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException(
                "The Avalonia test application is not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(
            colorToken.Value,
            themeVariant,
            out resourceOrNull);
        Assert.True(
            hasResource,
            "The product color token could not be resolved: " + colorToken.Value);

        SolidColorBrush? brushOrNull = resourceOrNull as SolidColorBrush;
        Assert.NotNull(brushOrNull);
        if (brushOrNull == null)
        {
            throw new InvalidOperationException(
                "The product color token is not a solid color brush: " +
                colorToken.Value);
        }

        return brushOrNull;
    }

    private static ContrastRatio calculateContrastRatio(
        Color foregroundColor,
        Color backgroundColor)
    {
        double foregroundLuminance = calculateRelativeLuminance(foregroundColor);
        double backgroundLuminance = calculateRelativeLuminance(backgroundColor);
        double lighterLuminance = Math.Max(
            foregroundLuminance,
            backgroundLuminance);
        double darkerLuminance = Math.Min(
            foregroundLuminance,
            backgroundLuminance);
        double contrastValue =
            (lighterLuminance + CONTRAST_LUMINANCE_OFFSET) /
            (darkerLuminance + CONTRAST_LUMINANCE_OFFSET);

        return new ContrastRatio(contrastValue);
    }

    private static double calculateRelativeLuminance(Color color)
    {
        double linearRed = calculateLinearColorChannel(color.R);
        double linearGreen = calculateLinearColorChannel(color.G);
        double linearBlue = calculateLinearColorChannel(color.B);

        return (RED_LUMINANCE_WEIGHT * linearRed) +
            (GREEN_LUMINANCE_WEIGHT * linearGreen) +
            (BLUE_LUMINANCE_WEIGHT * linearBlue);
    }

    private static double calculateLinearColorChannel(byte colorChannel)
    {
        double normalizedChannel = colorChannel / COLOR_CHANNEL_MAXIMUM;
        if (normalizedChannel <= SRGB_LINEAR_THRESHOLD)
        {
            return normalizedChannel / SRGB_LINEAR_DIVISOR;
        }

        double adjustedChannel =
            (normalizedChannel + SRGB_CURVE_OFFSET) /
            SRGB_CURVE_DIVISOR;
        return Math.Pow(adjustedChannel, SRGB_CURVE_EXPONENT);
    }

    private readonly record struct ColorToken(string Value);

    private readonly record struct ContrastRatio(double Value)
    {
        public bool IsAtLeast(ContrastRatio minimum)
        {
            return Value >= minimum.Value;
        }
    }

    private readonly record struct ContrastRequirement(
        ThemeVariant ThemeVariant,
        ColorToken Foreground,
        ColorToken Background,
        ContrastRatio Minimum);
}
