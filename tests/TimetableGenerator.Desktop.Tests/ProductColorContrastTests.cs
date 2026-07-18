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
        new ColorToken("ProductPrimaryActionFillBrush");
    private static readonly ColorToken ACCENT_FILL_HOVER =
        new ColorToken("ProductPrimaryActionHoverFillBrush");
    private static readonly ColorToken ACCENT_FILL_PRESSED =
        new ColorToken("ProductPrimaryActionPressedFillBrush");
    private static readonly ColorToken ACCENT_HOVER =
        new ColorToken("AccentHoverBrush");
    private static readonly ColorToken ACCENT_PRESSED =
        new ColorToken("AccentPressedBrush");
    private static readonly ColorToken ACCENT_TINT =
        new ColorToken("AccentTintBrush");
    private static readonly ColorToken SELECTION_SURFACE =
        new ColorToken("SelectionSurfaceBrush");
    private static readonly ColorToken SELECTION_HOVER_SURFACE =
        new ColorToken("SelectionHoverSurfaceBrush");
    private static readonly ColorToken SELECTION_PRESSED_SURFACE =
        new ColorToken("SelectionPressedSurfaceBrush");
    private static readonly ColorToken SELECTION_INDICATOR =
        new ColorToken("SelectionIndicatorBrush");
    private static readonly ColorToken CONTROL_BORDER =
        new ColorToken("ControlBorderBrush");
    private static readonly ColorToken CONTROL_HOVER_SURFACE =
        new ColorToken("ControlHoverSurfaceBrush");
    private static readonly ColorToken CONTROL_SURFACE =
        new ColorToken("ControlSurfaceBrush");
    private static readonly ColorToken CAPTION_CLOSE_HOVER_BACKGROUND =
        new ColorToken("CaptionCloseButtonHoverBackgroundBrush");
    private static readonly ColorToken CAPTION_CLOSE_PRESSED_BACKGROUND =
        new ColorToken("CaptionCloseButtonPressedBackgroundBrush");
    private static readonly ColorToken CAPTION_FOREGROUND =
        new ColorToken("CaptionButtonForeground");
    private static readonly ColorToken COURSE_BLUE_BACKGROUND =
        new ColorToken("CourseBlueBackgroundBrush");
    private static readonly ColorToken COURSE_BLUE_BORDER =
        new ColorToken("CourseBlueBorderBrush");
    private static readonly ColorToken COURSE_GREEN_BACKGROUND =
        new ColorToken("CourseGreenBackgroundBrush");
    private static readonly ColorToken COURSE_GREEN_BORDER =
        new ColorToken("CourseGreenBorderBrush");
    private static readonly ColorToken COURSE_PURPLE_BACKGROUND =
        new ColorToken("CoursePurpleBackgroundBrush");
    private static readonly ColorToken COURSE_PURPLE_BORDER =
        new ColorToken("CoursePurpleBorderBrush");
    private static readonly ColorToken ELEVATED_SURFACE =
        new ColorToken("ElevatedSurfaceBrush");
    private static readonly ColorToken ERROR =
        new ColorToken("ErrorBrush");
    private static readonly ColorToken ERROR_FILL =
        new ColorToken("ProductDangerActionFillBrush");
    private static readonly ColorToken ERROR_FILL_HOVER =
        new ColorToken("ProductDangerActionHoverFillBrush");
    private static readonly ColorToken ERROR_FILL_PRESSED =
        new ColorToken("ProductDangerActionPressedFillBrush");
    private static readonly ColorToken ERROR_SUBTLE =
        new ColorToken("ErrorSubtleBrush");
    private static readonly ColorToken FOCUS_ON_FILL_STROKE =
        new ColorToken("ProductFocusOnFillStrokeBrush");
    private static readonly ColorToken FOCUS_STROKE =
        new ColorToken("ProductFocusStrokeBrush");
    private static readonly ColorToken HOVER_SURFACE =
        new ColorToken("HoverSurfaceBrush");
    private static readonly ColorToken ON_ACCENT_FILL =
        new ColorToken("ProductOnPrimaryActionFillBrush");
    private static readonly ColorToken ON_ERROR_FILL =
        new ColorToken("ProductOnDangerActionFillBrush");
    private static readonly ColorToken PANE_SURFACE =
        new ColorToken("PaneSurfaceBrush");
    private static readonly ColorToken OVERLAY_SCRIM =
        new ColorToken("OverlayScrimBrush");
    private static readonly ColorToken PERSONAL_SCHEDULE_BACKGROUND =
        new ColorToken("PersonalScheduleBackgroundBrush");
    private static readonly ColorToken PERSONAL_SCHEDULE_BORDER =
        new ColorToken("PersonalScheduleBorderBrush");
    private static readonly ColorToken PRESSED_SURFACE =
        new ColorToken("PressedSurfaceBrush");
    private static readonly ColorToken SUBTLE_SURFACE =
        new ColorToken("SubtleSurfaceBrush");
    private static readonly ColorToken SUCCESS =
        new ColorToken("SuccessBrush");
    private static readonly ColorToken SUCCESS_SUBTLE =
        new ColorToken("SuccessSubtleBrush");
    private static readonly ColorToken SURFACE =
        new ColorToken("SurfaceBrush");
    private static readonly ColorToken SCHEDULE_HOUR_GRID_LINE =
        new ColorToken("ScheduleHourGridLineBrush");
    private static readonly ColorToken SCHEDULE_HALF_HOUR_GRID_LINE =
        new ColorToken("ScheduleHalfHourGridLineBrush");
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
        new ColorToken("ControlHoverSurfaceBrush"),
        new ColorToken("SubtleSurfaceBrush"),
        new ColorToken("HoverSurfaceBrush"),
        new ColorToken("PressedSurfaceBrush"),
        new ColorToken("BorderBrush"),
        new ColorToken("PaneDividerBrush"),
        new ColorToken("StrongBorderBrush"),
        new ColorToken("ControlBorderBrush"),
        new ColorToken("ScheduleHourGridLineBrush"),
        new ColorToken("ScheduleHalfHourGridLineBrush"),
        new ColorToken("BrandMarkBackgroundBrush"),
        new ColorToken("BrandMarkBorderBrush"),
        new ColorToken("TextPrimaryBrush"),
        new ColorToken("TextSecondaryBrush"),
        new ColorToken("TextTertiaryBrush"),
        new ColorToken("AccentBrush"),
        new ColorToken("AccentHoverBrush"),
        new ColorToken("AccentPressedBrush"),
        new ColorToken("AccentTintBrush"),
        new ColorToken("SelectionSurfaceBrush"),
        new ColorToken("SelectionHoverSurfaceBrush"),
        new ColorToken("SelectionPressedSurfaceBrush"),
        new ColorToken("SelectionIndicatorBrush"),
        new ColorToken("ProductPrimaryActionFillBrush"),
        new ColorToken("ProductPrimaryActionHoverFillBrush"),
        new ColorToken("ProductPrimaryActionPressedFillBrush"),
        new ColorToken("ProductOnPrimaryActionFillBrush"),
        new ColorToken("ProductFocusStrokeBrush"),
        new ColorToken("ProductFocusOnFillStrokeBrush"),
        new ColorToken("SuccessBrush"),
        new ColorToken("SuccessSubtleBrush"),
        new ColorToken("WarningBrush"),
        new ColorToken("WarningSubtleBrush"),
        new ColorToken("ErrorBrush"),
        new ColorToken("ErrorSubtleBrush"),
        new ColorToken("ProductDangerActionFillBrush"),
        new ColorToken("ProductDangerActionHoverFillBrush"),
        new ColorToken("ProductDangerActionPressedFillBrush"),
        new ColorToken("ProductOnDangerActionFillBrush"),
        new ColorToken("CourseBlueBackgroundBrush"),
        new ColorToken("CourseBlueBorderBrush"),
        new ColorToken("CoursePurpleBackgroundBrush"),
        new ColorToken("CoursePurpleBorderBrush"),
        new ColorToken("CourseGreenBackgroundBrush"),
        new ColorToken("CourseGreenBorderBrush"),
        new ColorToken("PersonalScheduleBackgroundBrush"),
        new ColorToken("PersonalScheduleBorderBrush"),
        new ColorToken("TitleBarBackgroundBrush"),
        new ColorToken("CaptionButtonBackground"),
        new ColorToken("CaptionButtonBorderBrush"),
        new ColorToken("CaptionButtonForeground"),
        new ColorToken("CaptionCloseButtonHoverBackgroundBrush"),
        new ColorToken("CaptionCloseButtonPressedBackgroundBrush"),
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
            bodyText(ThemeVariant.Light, TEXT_SECONDARY, SELECTION_SURFACE),
            bodyText(
                ThemeVariant.Light,
                TEXT_SECONDARY,
                SELECTION_HOVER_SURFACE),
            bodyText(
                ThemeVariant.Light,
                TEXT_SECONDARY,
                SELECTION_PRESSED_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_TERTIARY, CONTROL_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, CONTROL_HOVER_SURFACE),
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
            bodyText(ThemeVariant.Dark, TEXT_SECONDARY, SELECTION_SURFACE),
            bodyText(
                ThemeVariant.Dark,
                TEXT_SECONDARY,
                SELECTION_HOVER_SURFACE),
            bodyText(
                ThemeVariant.Dark,
                TEXT_SECONDARY,
                SELECTION_PRESSED_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_TERTIARY, CONTROL_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, CONTROL_HOVER_SURFACE),
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
    public void CaptionCloseIconMeetsContrastOnRealHoverAndPressedFills()
    {
        ContrastRequirement[] contrastRequirements =
        {
            nonText(
                ThemeVariant.Light,
                CAPTION_FOREGROUND,
                CAPTION_CLOSE_HOVER_BACKGROUND),
            nonText(
                ThemeVariant.Light,
                CAPTION_FOREGROUND,
                CAPTION_CLOSE_PRESSED_BACKGROUND),
            nonText(
                ThemeVariant.Dark,
                CAPTION_FOREGROUND,
                CAPTION_CLOSE_HOVER_BACKGROUND),
            nonText(
                ThemeVariant.Dark,
                CAPTION_FOREGROUND,
                CAPTION_CLOSE_PRESSED_BACKGROUND),
        };

        assertContrastRequirements(contrastRequirements);
    }

    [AvaloniaFact]
    public void InputHoverAndScheduleGridTokensKeepARestrainedVisualHierarchy()
    {
        assertThemePaletteHierarchy(ThemeVariant.Light);
        assertThemePaletteHierarchy(ThemeVariant.Dark);
    }

    [AvaloniaFact]
    public void OverlayScrimsPreserveModalFocusWithoutObscuringContext()
    {
        SolidColorBrush lightScrim = findRequiredBrush(
            OVERLAY_SCRIM,
            ThemeVariant.Light);
        SolidColorBrush darkScrim = findRequiredBrush(
            OVERLAY_SCRIM,
            ThemeVariant.Dark);

        Assert.Equal(0x52, lightScrim.Color.A);
        Assert.Equal(0x80, darkScrim.Color.A);
    }

    [AvaloniaFact]
    public void PrimaryActionPaletteUsesCalmProductBluesInBothThemes()
    {
        SolidColorBrush lightFill = findRequiredBrush(
            ACCENT_FILL,
            ThemeVariant.Light);
        SolidColorBrush darkFill = findRequiredBrush(
            ACCENT_FILL,
            ThemeVariant.Dark);
        SolidColorBrush darkHover = findRequiredBrush(
            ACCENT_FILL_HOVER,
            ThemeVariant.Dark);
        SolidColorBrush darkPressed = findRequiredBrush(
            ACCENT_FILL_PRESSED,
            ThemeVariant.Dark);

        Assert.Equal(Color.Parse("#0A60C8"), lightFill.Color);
        Assert.Equal(Color.Parse("#1B63C9"), darkFill.Color);
        Assert.Equal(Color.Parse("#236ED4"), darkHover.Color);
        Assert.Equal(Color.Parse("#154C9D"), darkPressed.Color);
    }

    [AvaloniaFact]
    public void SelectedSurfacesUseAConsistentModernBlueHierarchy()
    {
        assertSelectionPalette(
            ThemeVariant.Light,
            Color.Parse("#E6F0FF"),
            Color.Parse("#D9E8FF"),
            Color.Parse("#CBDEFF"),
            Color.Parse("#0A60C8"));
        assertSelectionPalette(
            ThemeVariant.Dark,
            Color.Parse("#182C49"),
            Color.Parse("#1E385C"),
            Color.Parse("#254672"),
            Color.Parse("#69A9FF"));
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
            bodyText(ThemeVariant.Light, SUCCESS, SUCCESS_SUBTLE),
            bodyText(ThemeVariant.Dark, WARNING, WARNING_SUBTLE),
            bodyText(ThemeVariant.Dark, WARNING, SURFACE),
            bodyText(ThemeVariant.Dark, ERROR, ERROR_SUBTLE),
            bodyText(ThemeVariant.Dark, ERROR, SURFACE),
            bodyText(ThemeVariant.Dark, SUCCESS, SURFACE),
            bodyText(ThemeVariant.Dark, SUCCESS, ACCENT_TINT),
            bodyText(ThemeVariant.Dark, SUCCESS, SUCCESS_SUBTLE),
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
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, SELECTION_SURFACE),
            bodyText(
                ThemeVariant.Light,
                TEXT_PRIMARY,
                SELECTION_HOVER_SURFACE),
            bodyText(
                ThemeVariant.Light,
                TEXT_PRIMARY,
                SELECTION_PRESSED_SURFACE),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, COURSE_BLUE_BACKGROUND),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, COURSE_PURPLE_BACKGROUND),
            bodyText(ThemeVariant.Light, TEXT_PRIMARY, COURSE_GREEN_BACKGROUND),
            bodyText(
                ThemeVariant.Light,
                TEXT_PRIMARY,
                PERSONAL_SCHEDULE_BACKGROUND),
            bodyText(ThemeVariant.Dark, ACCENT, PANE_SURFACE),
            bodyText(ThemeVariant.Dark, ACCENT, SURFACE),
            bodyText(ThemeVariant.Dark, ACCENT_HOVER, PANE_SURFACE),
            bodyText(ThemeVariant.Dark, ACCENT_PRESSED, PANE_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, ACCENT_TINT),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, SELECTION_SURFACE),
            bodyText(
                ThemeVariant.Dark,
                TEXT_PRIMARY,
                SELECTION_HOVER_SURFACE),
            bodyText(
                ThemeVariant.Dark,
                TEXT_PRIMARY,
                SELECTION_PRESSED_SURFACE),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, COURSE_BLUE_BACKGROUND),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, COURSE_PURPLE_BACKGROUND),
            bodyText(ThemeVariant.Dark, TEXT_PRIMARY, COURSE_GREEN_BACKGROUND),
            bodyText(
                ThemeVariant.Dark,
                TEXT_PRIMARY,
                PERSONAL_SCHEDULE_BACKGROUND),
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
            nonText(
                ThemeVariant.Light,
                SELECTION_INDICATOR,
                SELECTION_SURFACE),
            nonText(
                ThemeVariant.Light,
                SELECTION_INDICATOR,
                SELECTION_HOVER_SURFACE),
            nonText(
                ThemeVariant.Light,
                SELECTION_INDICATOR,
                SELECTION_PRESSED_SURFACE),
            nonText(
                ThemeVariant.Dark,
                SELECTION_INDICATOR,
                SELECTION_SURFACE),
            nonText(
                ThemeVariant.Dark,
                SELECTION_INDICATOR,
                SELECTION_HOVER_SURFACE),
            nonText(
                ThemeVariant.Dark,
                SELECTION_INDICATOR,
                SELECTION_PRESSED_SURFACE),
            nonText(
                ThemeVariant.Light,
                FOCUS_STROKE,
                SELECTION_SURFACE),
            nonText(
                ThemeVariant.Light,
                FOCUS_STROKE,
                SELECTION_HOVER_SURFACE),
            nonText(
                ThemeVariant.Light,
                FOCUS_STROKE,
                SELECTION_PRESSED_SURFACE),
            nonText(
                ThemeVariant.Dark,
                FOCUS_STROKE,
                SELECTION_SURFACE),
            nonText(
                ThemeVariant.Dark,
                FOCUS_STROKE,
                SELECTION_HOVER_SURFACE),
            nonText(
                ThemeVariant.Dark,
                FOCUS_STROKE,
                SELECTION_PRESSED_SURFACE),
            nonText(ThemeVariant.Light, FOCUS_ON_FILL_STROKE, ACCENT_FILL),
            nonText(ThemeVariant.Light, FOCUS_ON_FILL_STROKE, ACCENT_FILL_HOVER),
            nonText(ThemeVariant.Light, FOCUS_ON_FILL_STROKE, ACCENT_FILL_PRESSED),
            nonText(ThemeVariant.Light, FOCUS_ON_FILL_STROKE, ERROR_FILL),
            nonText(ThemeVariant.Dark, FOCUS_ON_FILL_STROKE, ACCENT_FILL),
            nonText(ThemeVariant.Dark, FOCUS_ON_FILL_STROKE, ACCENT_FILL_HOVER),
            nonText(ThemeVariant.Dark, FOCUS_ON_FILL_STROKE, ACCENT_FILL_PRESSED),
            nonText(ThemeVariant.Dark, FOCUS_ON_FILL_STROKE, ERROR_FILL),
            nonText(
                ThemeVariant.Light,
                COURSE_BLUE_BORDER,
                COURSE_BLUE_BACKGROUND),
            nonText(
                ThemeVariant.Light,
                COURSE_PURPLE_BORDER,
                COURSE_PURPLE_BACKGROUND),
            nonText(
                ThemeVariant.Light,
                COURSE_GREEN_BORDER,
                COURSE_GREEN_BACKGROUND),
            nonText(
                ThemeVariant.Light,
                PERSONAL_SCHEDULE_BORDER,
                PERSONAL_SCHEDULE_BACKGROUND),
            nonText(
                ThemeVariant.Dark,
                COURSE_BLUE_BORDER,
                COURSE_BLUE_BACKGROUND),
            nonText(
                ThemeVariant.Dark,
                COURSE_PURPLE_BORDER,
                COURSE_PURPLE_BACKGROUND),
            nonText(
                ThemeVariant.Dark,
                COURSE_GREEN_BORDER,
                COURSE_GREEN_BACKGROUND),
            nonText(
                ThemeVariant.Dark,
                PERSONAL_SCHEDULE_BORDER,
                PERSONAL_SCHEDULE_BACKGROUND),
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

    private static void assertSelectionPalette(
        ThemeVariant themeVariant,
        Color expectedSurface,
        Color expectedHoverSurface,
        Color expectedPressedSurface,
        Color expectedIndicator)
    {
        Assert.Equal(
            expectedSurface,
            findRequiredBrush(SELECTION_SURFACE, themeVariant).Color);
        Assert.Equal(
            expectedHoverSurface,
            findRequiredBrush(SELECTION_HOVER_SURFACE, themeVariant).Color);
        Assert.Equal(
            expectedPressedSurface,
            findRequiredBrush(SELECTION_PRESSED_SURFACE, themeVariant).Color);
        Assert.Equal(
            expectedIndicator,
            findRequiredBrush(SELECTION_INDICATOR, themeVariant).Color);
    }

    private static void assertThemePaletteHierarchy(ThemeVariant themeVariant)
    {
        Color controlSurface = findRequiredBrush(
            CONTROL_SURFACE,
            themeVariant).Color;
        Color controlHoverSurface = findRequiredBrush(
            CONTROL_HOVER_SURFACE,
            themeVariant).Color;
        Color surface = findRequiredBrush(SURFACE, themeVariant).Color;
        Color hourGridLine = findRequiredBrush(
            SCHEDULE_HOUR_GRID_LINE,
            themeVariant).Color;
        Color halfHourGridLine = findRequiredBrush(
            SCHEDULE_HALF_HOUR_GRID_LINE,
            themeVariant).Color;

        Assert.NotEqual(controlSurface, controlHoverSurface);
        ContrastRatio hourLineContrast = calculateContrastRatio(
            hourGridLine,
            surface);
        ContrastRatio halfHourLineContrast = calculateContrastRatio(
            halfHourGridLine,
            surface);
        Assert.True(hourLineContrast.Value > halfHourLineContrast.Value);
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
