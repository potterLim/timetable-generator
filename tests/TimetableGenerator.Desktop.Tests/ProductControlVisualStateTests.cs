using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ProductControlVisualStateTests
{
    private static readonly ControlStyleClass PRIMARY_ACTION_STYLE = new ControlStyleClass("accent");
    private static readonly ControlStyleClass DANGER_ACTION_STYLE = new ControlStyleClass("danger");
    private static readonly ControlStyleClass OUTLINE_ACTION_STYLE = new ControlStyleClass("outline");
    private static readonly ControlStyleClass BUTTON_CONTENT_STYLE = new ControlStyleClass("button-content");
    private static readonly ControlThemeToken CAPTION_BUTTON_THEME = new ControlThemeToken("ProductCaptionButtonTheme");

    private static readonly ColorToken PRIMARY_ACTION_FILL = new ColorToken("ProductPrimaryActionFillBrush");
    private static readonly ColorToken ON_PRIMARY_ACTION_FILL = new ColorToken("ProductOnPrimaryActionFillBrush");
    private static readonly ColorToken DANGER_ACTION_FILL = new ColorToken("ProductDangerActionFillBrush");
    private static readonly ColorToken CONTROL_BORDER = new ColorToken("ControlBorderBrush");
    private static readonly ColorToken CONTROL_SURFACE = new ColorToken("ControlSurfaceBrush");
    private static readonly ColorToken CONTROL_HOVER_SURFACE = new ColorToken("ControlHoverSurfaceBrush");
    private static readonly ColorToken HOVER_SURFACE = new ColorToken("HoverSurfaceBrush");
    private static readonly ColorToken PRESSED_SURFACE = new ColorToken("PressedSurfaceBrush");
    private static readonly ColorToken PRODUCT_FOCUS_STROKE = new ColorToken("ProductFocusStrokeBrush");
    private static readonly ColorToken CAPTION_CLOSE_HOVER_BACKGROUND = new ColorToken("CaptionCloseButtonHoverBackgroundBrush");
    private static readonly ColorToken CAPTION_CLOSE_PRESSED_BACKGROUND = new ColorToken("CaptionCloseButtonPressedBackgroundBrush");
    private static readonly ColorToken CAPTION_CLOSE_FOREGROUND = new ColorToken("CaptionCloseButtonForegroundBrush");
    private static readonly ColorToken CAPTION_BACKGROUND = new ColorToken("CaptionButtonBackground");
    private static readonly ColorToken CAPTION_PRESSED_BACKGROUND = new ColorToken("CaptionButtonBorderBrush");
    private static readonly ColorToken FOCUS_ON_FILL_STROKE = new ColorToken("ProductFocusOnFillStrokeBrush");

    [AvaloniaFact]
    public void ProductPrimaryActionUsesFilledBrandColorForApplicationDarkTheme()
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException("The Avalonia test application was not initialized.");
        }

        ThemeVariant? previousThemeVariantOrNull = applicationOrNull.RequestedThemeVariant;
        Button primaryAction = createButton(PRIMARY_ACTION_STYLE);
        Window window = new Window();
        window.Content = primaryAction;

        try
        {
            applicationOrNull.RequestedThemeVariant = ThemeVariant.Dark;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            assertButtonBrush(primaryAction.Background, PRIMARY_ACTION_FILL, ThemeVariant.Dark);
            assertRenderedButtonBackground(primaryAction, PRIMARY_ACTION_FILL, ThemeVariant.Dark);
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

            assertButtonBrush(primaryAction.Background, PRIMARY_ACTION_FILL, ThemeVariant.Light);
            assertRenderedButtonBackground(primaryAction, PRIMARY_ACTION_FILL, ThemeVariant.Light);
            assertButtonBrush(primaryAction.Foreground, ON_PRIMARY_ACTION_FILL, ThemeVariant.Light);
            assertButtonBrush(dangerAction.Background, DANGER_ACTION_FILL, ThemeVariant.Light);
            assertRenderedButtonBackground(dangerAction, DANGER_ACTION_FILL, ThemeVariant.Light);
            assertButtonBrush(outlineAction.BorderBrush, CONTROL_BORDER, ThemeVariant.Light);

            window.RequestedThemeVariant = ThemeVariant.Dark;
            Dispatcher.UIThread.RunJobs();

            assertButtonBrush(primaryAction.Background, PRIMARY_ACTION_FILL, ThemeVariant.Dark);
            assertRenderedButtonBackground(primaryAction, PRIMARY_ACTION_FILL, ThemeVariant.Dark);
            assertButtonBrush(primaryAction.Foreground, ON_PRIMARY_ACTION_FILL, ThemeVariant.Dark);
            assertButtonBrush(dangerAction.Background, DANGER_ACTION_FILL, ThemeVariant.Dark);
            assertRenderedButtonBackground(dangerAction, DANGER_ACTION_FILL, ThemeVariant.Dark);
            assertButtonBrush(outlineAction.BorderBrush, CONTROL_BORDER, ThemeVariant.Dark);

            bool isPrimaryActionFocused = primaryAction.Focus();
            Assert.True(isPrimaryActionFocused);
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(primaryAction.Background, PRIMARY_ACTION_FILL, ThemeVariant.Dark);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void OutlineActionUsesCompleteVisualStatesAcrossThemes()
    {
        Button outlineAction = createButton(OUTLINE_ACTION_STYLE);
        Button focusAnchor = new Button();
        focusAnchor.Content = "focus anchor";
        StackPanel controls = new StackPanel();
        controls.Spacing = 8.0;
        controls.Children.Add(outlineAction);
        controls.Children.Add(focusAnchor);
        Border layoutSurface = new Border();
        layoutSurface.Padding = new Thickness(40.0);
        layoutSurface.Child = controls;
        Window window = new Window();
        window.Width = 280.0;
        window.Height = 140.0;
        window.Content = layoutSurface;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ThemeVariant[] themeVariants =
            {
                ThemeVariant.Light,
                ThemeVariant.Dark,
            };
            foreach (ThemeVariant themeVariant in themeVariants)
            {
                window.RequestedThemeVariant = themeVariant;
                outlineAction.IsEnabled = true;
                window.MouseMove(new Point(1.0, 1.0), RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();

                assertOutlineActionVisuals(
                    outlineAction,
                    themeVariant,
                    CONTROL_SURFACE,
                    CONTROL_BORDER,
                    new Thickness(1.0));

                Point actionCenter = findControlCenter(window, outlineAction);
                window.MouseMove(actionCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertOutlineActionVisuals(
                    outlineAction,
                    themeVariant,
                    HOVER_SURFACE,
                    CONTROL_BORDER,
                    new Thickness(1.0));

                window.MouseDown(actionCenter, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertOutlineActionVisuals(
                    outlineAction,
                    themeVariant,
                    PRESSED_SURFACE,
                    CONTROL_BORDER,
                    new Thickness(1.0));
                window.MouseUp(actionCenter, MouseButton.Left, RawInputModifiers.None);

                window.MouseMove(new Point(1.0, 1.0), RawInputModifiers.None);
                Assert.True(focusAnchor.Focus(NavigationMethod.Tab));
                Assert.True(outlineAction.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                assertOutlineActionVisuals(
                    outlineAction,
                    themeVariant,
                    CONTROL_SURFACE,
                    PRODUCT_FOCUS_STROKE,
                    new Thickness(2.0));

                outlineAction.IsEnabled = false;
                window.MouseMove(actionCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(outlineAction.IsEffectivelyEnabled);
                assertOutlineActionVisuals(
                    outlineAction,
                    themeVariant,
                    CONTROL_SURFACE,
                    CONTROL_BORDER,
                    new Thickness(1.0));
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void LightInputControlsExposeRestrainedHoverFeedback()
    {
        TextBox textBox = new TextBox();
        textBox.Text = "과목 검색";
        ComboBox comboBox = new ComboBox();
        comboBox.ItemsSource = new string[] { "개설 단위 전체" };
        comboBox.SelectedIndex = 0;
        StackPanel controls = new StackPanel();
        controls.Children.Add(textBox);
        controls.Children.Add(comboBox);

        Window window = new Window();
        window.RequestedThemeVariant = ThemeVariant.Light;
        window.Content = controls;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            assertButtonBrush(textBox.Background, CONTROL_SURFACE, ThemeVariant.Light);
            assertButtonBrush(comboBox.Background, CONTROL_SURFACE, ThemeVariant.Light);

            movePointerToControl(window, textBox);
            assertButtonBrush(textBox.Background, CONTROL_HOVER_SURFACE, ThemeVariant.Light);

            movePointerToControl(window, comboBox);
            assertButtonBrush(comboBox.Background, CONTROL_HOVER_SURFACE, ThemeVariant.Light);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ButtonContentPatternCentersIconAndTextOnOneAxis()
    {
        Border icon = new Border();
        icon.Width = 16.0;
        icon.Height = 16.0;
        TextBlock label = new TextBlock();
        label.Text = "PNG로 저장";
        StackPanel content = new StackPanel();
        content.Classes.Add(BUTTON_CONTENT_STYLE.Value);
        content.Children.Add(icon);
        content.Children.Add(label);
        Button button = new Button();
        button.Content = content;

        Window window = new Window();
        window.Content = button;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(Orientation.Horizontal, content.Orientation);
            Assert.Equal(7.0, content.Spacing);
            Assert.Equal(VerticalAlignment.Center, content.VerticalAlignment);
            Assert.Equal(VerticalAlignment.Center, icon.VerticalAlignment);
            Assert.Equal(VerticalAlignment.Center, label.VerticalAlignment);
            assertControlsShareVerticalCenter(content, icon, label);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProductActionVariantsShareTheSameRenderedHeight()
    {
        Button primaryButton = createButton(PRIMARY_ACTION_STYLE);
        Button outlineButton = createButton(OUTLINE_ACTION_STYLE);
        Button dangerButton = createButton(DANGER_ACTION_STYLE);
        StackPanel actions = new StackPanel();
        actions.Orientation = Orientation.Horizontal;
        actions.Spacing = 8.0;
        actions.VerticalAlignment = VerticalAlignment.Center;
        actions.Children.Add(outlineButton);
        actions.Children.Add(dangerButton);
        actions.Children.Add(primaryButton);

        Window window = new Window();
        window.Content = actions;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(40.0, primaryButton.Bounds.Height);
            Assert.Equal(primaryButton.Bounds.Height, outlineButton.Bounds.Height);
            Assert.Equal(primaryButton.Bounds.Height, dangerButton.Bounds.Height);
            Assert.Equal(VerticalAlignment.Center, primaryButton.VerticalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, outlineButton.VerticalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, dangerButton.VerticalContentAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProductCaptionButtonPreservesCenteredGeometryAcrossVisualStates()
    {
        ThemeVariant[] themeVariants =
        {
            ThemeVariant.Light,
            ThemeVariant.Dark,
        };
        foreach (ThemeVariant themeVariant in themeVariants)
        {
            assertCaptionButtonVisualStates(themeVariant);
        }
    }

    [AvaloniaFact]
    public void CaptionCloseButtonRendersAccessibleStatesAcrossThemes()
    {
        ThemeVariant[] themeVariants =
        {
            ThemeVariant.Light,
            ThemeVariant.Dark,
        };
        foreach (ThemeVariant themeVariant in themeVariants)
        {
            assertCaptionCloseButtonVisualStates(themeVariant);
        }
    }

    private static void assertCaptionButtonVisualStates(ThemeVariant themeVariant)
    {
        Border glyph = new Border();
        glyph.Width = 11.0;
        glyph.Height = 1.0;
        Viewbox glyphViewbox = new Viewbox();
        glyphViewbox.Width = 11.0;
        glyphViewbox.Height = 11.0;
        glyphViewbox.HorizontalAlignment = HorizontalAlignment.Center;
        glyphViewbox.VerticalAlignment = VerticalAlignment.Center;
        glyphViewbox.Child = glyph;
        Button captionButton = new Button();
        captionButton.Classes.Add("caption-button");
        captionButton.Theme = findRequiredControlTheme(CAPTION_BUTTON_THEME, themeVariant);
        captionButton.Content = glyphViewbox;

        Window window = new Window();
        window.Width = 120.0;
        window.Height = 80.0;
        window.RequestedThemeVariant = themeVariant;
        window.Content = captionButton;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(45.0, captionButton.Bounds.Width);
            Assert.Equal(30.0, captionButton.Bounds.Height);
            Assert.Equal(0.0, captionButton.MinWidth);
            Assert.Equal(0.0, captionButton.MinHeight);
            Assert.Equal(new Thickness(0.0), captionButton.Padding);
            Assert.Equal(HorizontalAlignment.Center, captionButton.HorizontalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, captionButton.VerticalContentAlignment);
            assertControlsShareVerticalCenter(captionButton, captionButton, glyphViewbox);

            Rect initialButtonBounds = captionButton.Bounds;
            Point initialGlyphCenter = findControlCenter(window, glyphViewbox);
            ContentPresenter captionSurface = captionButton
                .GetVisualDescendants()
                .OfType<ContentPresenter>()
                .Single(control => control.Name == "PART_ContentPresenter");

            Assert.True(captionButton.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(captionSurface.BorderBrush, PRODUCT_FOCUS_STROKE, themeVariant);
            Assert.Equal(new Thickness(2.0), captionSurface.BorderThickness);
            assertCaptionGeometryIsUnchanged(
                window,
                captionButton,
                glyphViewbox,
                initialButtonBounds,
                initialGlyphCenter);

            Point captionButtonCenter = findControlCenter(window, captionButton);
            window.MouseMove(captionButtonCenter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(captionSurface.Background, CAPTION_BACKGROUND, themeVariant);
            assertCaptionGeometryIsUnchanged(
                window,
                captionButton,
                glyphViewbox,
                initialButtonBounds,
                initialGlyphCenter);

            window.MouseDown(captionButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(captionSurface.Background, CAPTION_PRESSED_BACKGROUND, themeVariant);
            assertCaptionGeometryIsUnchanged(
                window,
                captionButton,
                glyphViewbox,
                initialButtonBounds,
                initialGlyphCenter);
            window.MouseUp(captionButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    private static void assertCaptionCloseButtonVisualStates(ThemeVariant themeVariant)
    {
        Button closeButton = new Button();
        closeButton.Classes.Add("caption-close-button");
        closeButton.Theme = findRequiredControlTheme(CAPTION_BUTTON_THEME, themeVariant);
        Path closeGlyph = new Path();
        closeGlyph.Width = 11.0;
        closeGlyph.Height = 11.0;
        closeGlyph.Stretch = Stretch.Uniform;
        closeGlyph.Data = Geometry.Parse(
            "M1169 1024l879 -879l-145 -145l-879 879l-879 -879" +
            "l-145 145l879 879l-879 879l145 145l879 -879" +
            "l879 879l145 -145z");
        closeGlyph.Bind(
            Shape.FillProperty,
            new Binding
            {
                Path = nameof(Button.Foreground),
                Source = closeButton,
            });
        closeButton.Content = closeGlyph;

        Window window = new Window();
        window.RequestedThemeVariant = themeVariant;
        window.Content = closeButton;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Point closeButtonCenter = findControlCenter(window, closeButton);
            ContentPresenter closeSurface = closeButton.GetVisualDescendants()
                .OfType<ContentPresenter>()
                .Single(control => control.Name == "PART_ContentPresenter");
            Rect initialButtonBounds = closeButton.Bounds;
            Point initialGlyphCenter = findControlCenter(window, closeGlyph);

            Assert.True(closeButton.Focus(NavigationMethod.Tab));
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(closeSurface.BorderBrush, PRODUCT_FOCUS_STROKE, themeVariant);
            Assert.Equal(new Thickness(2.0), closeSurface.BorderThickness);
            assertCaptionGeometryIsUnchanged(
                window,
                closeButton,
                closeGlyph,
                initialButtonBounds,
                initialGlyphCenter);

            window.MouseMove(closeButtonCenter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(closeSurface.Background, CAPTION_CLOSE_HOVER_BACKGROUND, themeVariant);
            assertButtonBrush(closeSurface.BorderBrush, FOCUS_ON_FILL_STROKE, themeVariant);
            assertButtonBrush(closeButton.Foreground, CAPTION_CLOSE_FOREGROUND, themeVariant);
            assertButtonBrush(closeGlyph.Fill, CAPTION_CLOSE_FOREGROUND, themeVariant);
            assertCaptionGeometryIsUnchanged(
                window,
                closeButton,
                closeGlyph,
                initialButtonBounds,
                initialGlyphCenter);

            window.MouseDown(closeButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(closeSurface.Background, CAPTION_CLOSE_PRESSED_BACKGROUND, themeVariant);
            assertButtonBrush(closeSurface.BorderBrush, FOCUS_ON_FILL_STROKE, themeVariant);
            assertButtonBrush(closeGlyph.Fill, CAPTION_CLOSE_FOREGROUND, themeVariant);
            assertCaptionGeometryIsUnchanged(
                window,
                closeButton,
                closeGlyph,
                initialButtonBounds,
                initialGlyphCenter);
            window.MouseUp(closeButtonCenter, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }

    private static void assertCaptionGeometryIsUnchanged(
        Window window,
        Button captionButton,
        Control glyph,
        Rect expectedButtonBounds,
        Point expectedGlyphCenter)
    {
        Assert.Equal(expectedButtonBounds, captionButton.Bounds);
        Point actualGlyphCenter = findControlCenter(window, glyph);
        Assert.InRange(Math.Abs(actualGlyphCenter.X - expectedGlyphCenter.X), 0.0, 0.01);
        Assert.InRange(Math.Abs(actualGlyphCenter.Y - expectedGlyphCenter.Y), 0.0, 0.01);
    }

    private static Button createButton(ControlStyleClass styleClass)
    {
        Button button = new Button();
        button.Classes.Add(styleClass.Value);
        button.Content = styleClass.Value;
        return button;
    }

    private static void movePointerToControl(Window window, Control control)
    {
        Point controlCenter = findControlCenter(window, control);
        window.MouseMove(controlCenter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(control.IsPointerOver);
    }

    private static Point findControlCenter(Window window, Control control)
    {
        Point? controlOriginOrNull = control.TranslatePoint(new Point(0.0, 0.0), window);
        Assert.NotNull(controlOriginOrNull);
        if (controlOriginOrNull == null)
        {
            throw new InvalidOperationException("The control position could not be resolved.");
        }

        return controlOriginOrNull.Value
            + new Vector(control.Bounds.Width / 2.0, control.Bounds.Height / 2.0);
    }

    private static void assertControlsShareVerticalCenter(
        Control root,
        Control firstControl,
        Control secondControl)
    {
        Point? firstOriginOrNull = firstControl.TranslatePoint(new Point(0.0, 0.0), root);
        Point? secondOriginOrNull = secondControl.TranslatePoint(new Point(0.0, 0.0), root);
        Assert.NotNull(firstOriginOrNull);
        Assert.NotNull(secondOriginOrNull);
        if (firstOriginOrNull == null || secondOriginOrNull == null)
        {
            throw new InvalidOperationException("The button content geometry could not be resolved.");
        }

        double firstCenterY = firstOriginOrNull.Value.Y
            + (firstControl.Bounds.Height / 2.0);
        double secondCenterY = secondOriginOrNull.Value.Y
            + (secondControl.Bounds.Height / 2.0);

        Assert.InRange(Math.Abs(firstCenterY - secondCenterY), 0.0, 0.5);
    }

    private static void assertButtonBrush(
        IBrush? actualBrushOrNull,
        ColorToken expectedColorToken,
        ThemeVariant themeVariant)
    {
        SolidColorBrush actualBrush = Assert.IsType<SolidColorBrush>(actualBrushOrNull);
        SolidColorBrush expectedBrush = findRequiredThemeBrush(expectedColorToken, themeVariant);
        Assert.Equal(expectedBrush.Color, actualBrush.Color);
    }

    private static void assertRenderedButtonBackground(
        Button button,
        ColorToken expectedColorToken,
        ThemeVariant themeVariant)
    {
        ContentPresenter contentPresenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single();
        assertButtonBrush(contentPresenter.Background, expectedColorToken, themeVariant);
    }

    private static void assertOutlineActionVisuals(
        Button button,
        ThemeVariant themeVariant,
        ColorToken backgroundToken,
        ColorToken borderToken,
        Thickness borderThickness)
    {
        assertButtonBrush(button.Background, backgroundToken, themeVariant);
        assertButtonBrush(button.BorderBrush, borderToken, themeVariant);
        Assert.Equal(borderThickness, button.BorderThickness);
        ContentPresenter presenter = button.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .Single(candidate => candidate.Name == "PART_ContentPresenter");
        assertButtonBrush(presenter.Background, backgroundToken, themeVariant);
        assertButtonBrush(presenter.BorderBrush, borderToken, themeVariant);
        Assert.Equal(borderThickness, presenter.BorderThickness);
    }

    private static SolidColorBrush findRequiredThemeBrush(
        ColorToken colorToken,
        ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException("The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(
            colorToken.Value,
            themeVariant,
            out resourceOrNull);
        Assert.True(hasResource, "Missing brush resource: " + colorToken.Value);
        return Assert.IsType<SolidColorBrush>(resourceOrNull);
    }

    private static ControlTheme findRequiredControlTheme(
        ControlThemeToken controlThemeToken,
        ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException("The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(
            controlThemeToken.Value,
            themeVariant,
            out resourceOrNull);
        Assert.True(hasResource, "Missing control theme: " + controlThemeToken.Value);

        return Assert.IsType<ControlTheme>(resourceOrNull);
    }

    private readonly record struct ColorToken(string Value);

    private readonly record struct ControlStyleClass(string Value);

    private readonly record struct ControlThemeToken(string Value);
}
