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

    private static readonly ColorToken ACCENT =
        new ColorToken("AccentBrush");
    private static readonly ColorToken ACCENT_FILL =
        new ColorToken("AccentFillBrush");
    private static readonly ColorToken ACCENT_FILL_HOVER =
        new ColorToken("AccentFillHoverBrush");
    private static readonly ColorToken ACCENT_FILL_PRESSED =
        new ColorToken("AccentFillPressedBrush");
    private static readonly ColorToken ACCENT_HOVER =
        new ColorToken("AccentHoverBrush");
    private static readonly ColorToken ACCENT_PRESSED =
        new ColorToken("AccentPressedBrush");
    private static readonly ColorToken ACCENT_TINT =
        new ColorToken("AccentTintBrush");
    private static readonly ColorToken CONTROL_BORDER =
        new ColorToken("ControlBorderBrush");
    private static readonly ColorToken CONTROL_SURFACE =
        new ColorToken("ControlSurfaceBrush");
    private static readonly ColorToken COURSE_BLUE_BACKGROUND =
        new ColorToken("CourseBlueBackgroundBrush");
    private static readonly ColorToken COURSE_GREEN_BACKGROUND =
        new ColorToken("CourseGreenBackgroundBrush");
    private static readonly ColorToken COURSE_PURPLE_BACKGROUND =
        new ColorToken("CoursePurpleBackgroundBrush");
    private static readonly ColorToken ELEVATED_SURFACE =
        new ColorToken("ElevatedSurfaceBrush");
    private static readonly ColorToken ERROR =
        new ColorToken("ErrorBrush");
    private static readonly ColorToken ERROR_FILL =
        new ColorToken("ErrorFillBrush");
    private static readonly ColorToken ERROR_FILL_HOVER =
        new ColorToken("ErrorFillHoverBrush");
    private static readonly ColorToken ERROR_FILL_PRESSED =
        new ColorToken("ErrorFillPressedBrush");
    private static readonly ColorToken ERROR_SUBTLE =
        new ColorToken("ErrorSubtleBrush");
    private static readonly ColorToken FOCUS_ON_FILL_STROKE =
        new ColorToken("FocusOnFillStrokeBrush");
    private static readonly ColorToken FOCUS_STROKE =
        new ColorToken("FocusStrokeBrush");
    private static readonly ColorToken HOVER_SURFACE =
        new ColorToken("HoverSurfaceBrush");
    private static readonly ColorToken ON_ACCENT_FILL =
        new ColorToken("OnAccentFillBrush");
    private static readonly ColorToken ON_ERROR_FILL =
        new ColorToken("OnErrorFillBrush");
    private static readonly ColorToken PANE_SURFACE =
        new ColorToken("PaneSurfaceBrush");
    private static readonly ColorToken PRESSED_SURFACE =
        new ColorToken("PressedSurfaceBrush");
    private static readonly ColorToken SUBTLE_SURFACE =
        new ColorToken("SubtleSurfaceBrush");
    private static readonly ColorToken SUCCESS =
        new ColorToken("SuccessBrush");
    private static readonly ColorToken SURFACE =
        new ColorToken("SurfaceBrush");
    private static readonly ColorToken TEXT_PRIMARY =
        new ColorToken("TextPrimaryBrush");
    private static readonly ColorToken TEXT_SECONDARY =
        new ColorToken("TextSecondaryBrush");
    private static readonly ColorToken TEXT_TERTIARY =
        new ColorToken("TextTertiaryBrush");
    private static readonly ColorToken WARNING =
        new ColorToken("WarningBrush");
    private static readonly ColorToken WARNING_SUBTLE =
        new ColorToken("WarningSubtleBrush");
    private static readonly ColorToken WINDOW_BACKGROUND =
        new ColorToken("WindowBackgroundBrush");

    private static readonly ContrastRatio MINIMUM_BODY_TEXT_CONTRAST =
        new ContrastRatio(4.5);
    private static readonly ContrastRatio MINIMUM_NON_TEXT_CONTRAST =
        new ContrastRatio(3.0);

    private static readonly ColorToken[] REQUIRED_SOLID_COLOR_TOKENS =
    {
        new ColorToken("WindowBackgroundBrush"),
        new ColorToken("ChromeSurfaceBrush"),
        new ColorToken("PaneSurfaceBrush"),
        new ColorToken("SurfaceBrush"),
        new ColorToken("ElevatedSurfaceBrush"),
        new ColorToken("ControlSurfaceBrush"),
        new ColorToken("SubtleSurfaceBrush"),
        new ColorToken("HoverSurfaceBrush"),
        new ColorToken("PressedSurfaceBrush"),
        new ColorToken("BorderBrush"),
        new ColorToken("PaneDividerBrush"),
        new ColorToken("StrongBorderBrush"),
        new ColorToken("ControlBorderBrush"),
        new ColorToken("BrandMarkBackgroundBrush"),
        new ColorToken("BrandMarkBorderBrush"),
        new ColorToken("TextPrimaryBrush"),
        new ColorToken("TextSecondaryBrush"),
        new ColorToken("TextTertiaryBrush"),
        new ColorToken("AccentBrush"),
        new ColorToken("AccentHoverBrush"),
        new ColorToken("AccentPressedBrush"),
        new ColorToken("AccentTintBrush"),
        new ColorToken("AccentFillBrush"),
        new ColorToken("AccentFillHoverBrush"),
        new ColorToken("AccentFillPressedBrush"),
        new ColorToken("OnAccentFillBrush"),
        new ColorToken("FocusStrokeBrush"),
        new ColorToken("FocusOnFillStrokeBrush"),
        new ColorToken("SuccessBrush"),
        new ColorToken("WarningBrush"),
        new ColorToken("WarningSubtleBrush"),
        new ColorToken("ErrorBrush"),
        new ColorToken("ErrorSubtleBrush"),
        new ColorToken("ErrorFillBrush"),
        new ColorToken("ErrorFillHoverBrush"),
        new ColorToken("ErrorFillPressedBrush"),
        new ColorToken("OnErrorFillBrush"),
        new ColorToken("CourseBlueBackgroundBrush"),
        new ColorToken("CourseBlueBorderBrush"),
        new ColorToken("CoursePurpleBackgroundBrush"),
        new ColorToken("CoursePurpleBorderBrush"),
        new ColorToken("CourseGreenBackgroundBrush"),
        new ColorToken("CourseGreenBorderBrush"),
        new ColorToken("TitleBarBackgroundBrush"),
        new ColorToken("CaptionButtonBackground"),
        new ColorToken("CaptionButtonBorderBrush"),
        new ColorToken("CaptionButtonForeground"),
    };

    [AvaloniaFact]
    public void ProductSolidColorTokenContractExistsInLightAndDarkThemes()
    {
        ThemeVariant[] themeVariants =
        {
            ThemeVariant.Light,
            ThemeVariant.Dark,
        };

        foreach (ThemeVariant themeVariant in themeVariants)
        {
            foreach (ColorToken colorToken in REQUIRED_SOLID_COLOR_TOKENS)
            {
                findRequiredBrush(colorToken, themeVariant);
            }
        }
    }

    [AvaloniaFact]
    public void BodyTextTokensMeetContrastOnRenderedSurfaces()
    {
        ContrastRequirement[] contrastRequirements =
        {
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, WINDOW_BACKGROUND),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, PANE_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, ELEVATED_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, SUBTLE_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_SECONDARY, WINDOW_BACKGROUND),
            bodyText(ThemeVariant.Light, TEXT_SECONDARY, PANE_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_SECONDARY, ELEVATED_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_SECONDARY, SUBTLE_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_SECONDARY, HOVER_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_SECONDARY, ACCENT_TINT),
            bodyText(ThemeVariant.Light, TEXT_TERTIARY, CONTROL_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, WINDOW_BACKGROUND),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, PANE_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, ELEVATED_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, SUBTLE_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_SECONDARY, WINDOW_BACKGROUND),
            bodyText(ThemeVariant.Dark, TEXT_SECONDARY, PANE_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_SECONDARY, ELEVATED_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_SECONDARY, SUBTLE_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_SECONDARY, HOVER_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_SECONDARY, ACCENT_TINT),
            bodyText(ThemeVariant.Dark, TEXT_TERTIARY, CONTROL_SURFACE),
        };

        assertContrastRequirements(contrastRequirements);
    }

    [AvaloniaFact]
    public void FilledActionTextMeetsContrastInEveryInteractionState()
    {
        ContrastRequirement[] contrastRequirements =
        {
            bodyText(ThemeVariant.Light, ON_ACCENT_FILL, ACCENT_FILL),
            bodyText(ThemeVariant.Light, ON_ACCENT_FILL, ACCENT_FILL_HOVER),
            bodyText(ThemeVariant.Light, ON_ACCENT_FILL, ACCENT_FILL_PRESSED),
            bodyText(ThemeVariant.Dark, ON_ACCENT_FILL, ACCENT_FILL),
            bodyText(ThemeVariant.Dark, ON_ACCENT_FILL, ACCENT_FILL_HOVER),
            bodyText(ThemeVariant.Dark, ON_ACCENT_FILL, ACCENT_FILL_PRESSED),
            bodyText(ThemeVariant.Light, ON_ERROR_FILL, ERROR_FILL),
            bodyText(ThemeVariant.Light, ON_ERROR_FILL, ERROR_FILL_HOVER),
            bodyText(ThemeVariant.Light, ON_ERROR_FILL, ERROR_FILL_PRESSED),
            bodyText(ThemeVariant.Dark, ON_ERROR_FILL, ERROR_FILL),
            bodyText(ThemeVariant.Dark, ON_ERROR_FILL, ERROR_FILL_HOVER),
            bodyText(ThemeVariant.Dark, ON_ERROR_FILL, ERROR_FILL_PRESSED),
        };

        assertContrastRequirements(contrastRequirements);
    }

    [AvaloniaFact]
    public void SemanticStatusTokensMeetContrastOnRenderedSurfaces()
    {
        ContrastRequirement[] contrastRequirements =
        {
            bodyText(ThemeVariant.Light, WARNING, WARNING_SUBTLE),
            bodyText(ThemeVariant.Light, WARNING, SURFACE),
            bodyText(ThemeVariant.Light, ERROR, ERROR_SUBTLE),
            bodyText(ThemeVariant.Light, ERROR, SURFACE),
            bodyText(ThemeVariant.Light, SUCCESS, SURFACE),
            bodyText(ThemeVariant.Light, SUCCESS, ACCENT_TINT),
            bodyText(ThemeVariant.Dark, WARNING, WARNING_SUBTLE),
            bodyText(ThemeVariant.Dark, WARNING, SURFACE),
            bodyText(ThemeVariant.Dark, ERROR, ERROR_SUBTLE),
            bodyText(ThemeVariant.Dark, ERROR, SURFACE),
            bodyText(ThemeVariant.Dark, SUCCESS, SURFACE),
            bodyText(ThemeVariant.Dark, SUCCESS, ACCENT_TINT),
        };

        assertContrastRequirements(contrastRequirements);
    }

    [AvaloniaFact]
    public void SelectionAndScheduleCardTextMeetContrastInBothThemes()
    {
        ContrastRequirement[] contrastRequirements =
        {
            bodyText(ThemeVariant.Light, ACCENT, PANE_SURFACE),
            bodyText(ThemeVariant.Light, ACCENT, SURFACE),
            bodyText(ThemeVariant.Light, ACCENT_HOVER, PANE_SURFACE),
            bodyText(ThemeVariant.Light, ACCENT_PRESSED, PANE_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, ACCENT_TINT),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, COURSE_BLUE_BACKGROUND),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, COURSE_PURPLE_BACKGROUND),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, COURSE_GREEN_BACKGROUND),
            bodyText(ThemeVariant.Dark, ACCENT, PANE_SURFACE),
            bodyText(ThemeVariant.Dark, ACCENT, SURFACE),
            bodyText(ThemeVariant.Dark, ACCENT_HOVER, PANE_SURFACE),
            bodyText(ThemeVariant.Dark, ACCENT_PRESSED, PANE_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, ACCENT_TINT),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, COURSE_BLUE_BACKGROUND),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, COURSE_PURPLE_BACKGROUND),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, COURSE_GREEN_BACKGROUND),
        };

        assertContrastRequirements(contrastRequirements);
    }

    [AvaloniaFact]
    public void InteractiveBoundariesAndFocusMeetNonTextContrast()
    {
        ContrastRequirement[] contrastRequirements =
        {
            nonText(ThemeVariant.Light, CONTROL_BORDER, CONTROL_SURFACE),
            nonText(ThemeVariant.Light, CONTROL_BORDER, PANE_SURFACE),
            nonText(ThemeVariant.Dark, CONTROL_BORDER, CONTROL_SURFACE),
            nonText(ThemeVariant.Dark, CONTROL_BORDER, PANE_SURFACE),
            nonText(ThemeVariant.Light, FOCUS_STROKE, WINDOW_BACKGROUND),
            nonText(ThemeVariant.Light, FOCUS_STROKE, PANE_SURFACE),
            nonText(ThemeVariant.Light, FOCUS_STROKE, HOVER_SURFACE),
            nonText(ThemeVariant.Light, FOCUS_STROKE, PRESSED_SURFACE),
            nonText(ThemeVariant.Dark, FOCUS_STROKE, WINDOW_BACKGROUND),
            nonText(ThemeVariant.Dark, FOCUS_STROKE, PANE_SURFACE),
            nonText(ThemeVariant.Dark, FOCUS_STROKE, HOVER_SURFACE),
            nonText(ThemeVariant.Dark, FOCUS_STROKE, PRESSED_SURFACE),
            nonText(ThemeVariant.Light, FOCUS_ON_FILL_STROKE, ACCENT_FILL),
            nonText(ThemeVariant.Light, FOCUS_ON_FILL_STROKE, ERROR_FILL),
            nonText(ThemeVariant.Dark, FOCUS_ON_FILL_STROKE, ACCENT_FILL),
            nonText(ThemeVariant.Dark, FOCUS_ON_FILL_STROKE, ERROR_FILL),
        };

        assertContrastRequirements(contrastRequirements);
    }

    private static ContrastRequirement bodyText(
        ThemeVariant themeVariant,
        ColorToken foreground,
        ColorToken background)
    {
        return new ContrastRequirement(
            themeVariant,
            foreground,
            background,
            MINIMUM_BODY_TEXT_CONTRAST);
    }

    private static ContrastRequirement nonText(
        ThemeVariant themeVariant,
        ColorToken foreground,
        ColorToken background)
    {
        return new ContrastRequirement(
            themeVariant,
            foreground,
            background,
            MINIMUM_NON_TEXT_CONTRAST);
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
