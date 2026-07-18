using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
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
    private static readonly ControlStyleClass PRIMARY_ACTION_STYLE =
        new ControlStyleClass("accent");
    private static readonly ControlStyleClass DANGER_ACTION_STYLE =
        new ControlStyleClass("danger");
    private static readonly ControlStyleClass OUTLINE_ACTION_STYLE =
        new ControlStyleClass("outline");
    private static readonly ControlStyleClass BUTTON_CONTENT_STYLE =
        new ControlStyleClass("button-content");
    private static readonly ControlThemeToken CAPTION_BUTTON_THEME =
        new ControlThemeToken("ProductCaptionButtonTheme");

    private static readonly ColorToken PRIMARY_ACTION_FILL =
        new ColorToken("ProductPrimaryActionFillBrush");
    private static readonly ColorToken ON_PRIMARY_ACTION_FILL =
        new ColorToken("ProductOnPrimaryActionFillBrush");
    private static readonly ColorToken DANGER_ACTION_FILL =
        new ColorToken("ProductDangerActionFillBrush");
    private static readonly ColorToken CONTROL_BORDER =
        new ColorToken("ControlBorderBrush");
    private static readonly ColorToken CONTROL_SURFACE =
        new ColorToken("ControlSurfaceBrush");
    private static readonly ColorToken CONTROL_HOVER_SURFACE =
        new ColorToken("ControlHoverSurfaceBrush");
    private static readonly ColorToken CAPTION_CLOSE_HOVER_BACKGROUND =
        new ColorToken("CaptionCloseButtonHoverBackgroundBrush");
    private static readonly ColorToken CAPTION_CLOSE_PRESSED_BACKGROUND =
        new ColorToken("CaptionCloseButtonPressedBackgroundBrush");
    private static readonly ColorToken CAPTION_FOREGROUND =
        new ColorToken("CaptionButtonForeground");

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

            assertButtonBrush(
                textBox.Background,
                CONTROL_SURFACE,
                ThemeVariant.Light);
            assertButtonBrush(
                comboBox.Background,
                CONTROL_SURFACE,
                ThemeVariant.Light);

            movePointerToControl(window, textBox);
            assertButtonBrush(
                textBox.Background,
                CONTROL_HOVER_SURFACE,
                ThemeVariant.Light);

            movePointerToControl(window, comboBox);
            assertButtonBrush(
                comboBox.Background,
                CONTROL_HOVER_SURFACE,
                ThemeVariant.Light);
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
            Assert.Equal(
                VerticalAlignment.Center,
                primaryButton.VerticalContentAlignment);
            Assert.Equal(
                VerticalAlignment.Center,
                outlineButton.VerticalContentAlignment);
            Assert.Equal(
                VerticalAlignment.Center,
                dangerButton.VerticalContentAlignment);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProductCaptionButtonUsesThirtyPixelCenteredGeometry()
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
        captionButton.Theme = findRequiredControlTheme(
            CAPTION_BUTTON_THEME,
            ThemeVariant.Light);
        captionButton.Content = glyphViewbox;

        Window window = new Window();
        window.Width = 120.0;
        window.Height = 80.0;
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
            Assert.Equal(
                HorizontalAlignment.Center,
                captionButton.HorizontalContentAlignment);
            Assert.Equal(
                VerticalAlignment.Center,
                captionButton.VerticalContentAlignment);
            assertControlsShareVerticalCenter(
                captionButton,
                captionButton,
                glyphViewbox);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DarkCaptionCloseButtonRendersTheAccessiblePressedComposite()
    {
        Button closeButton = new Button();
        closeButton.Theme = findRequiredControlTheme(
            CAPTION_BUTTON_THEME,
            ThemeVariant.Dark);
        closeButton.Background = findRequiredThemeBrush(
            CAPTION_CLOSE_HOVER_BACKGROUND,
            ThemeVariant.Dark);
        closeButton.BorderBrush = findRequiredThemeBrush(
            CAPTION_CLOSE_PRESSED_BACKGROUND,
            ThemeVariant.Dark);
        Path closeGlyph = new Path();
        closeGlyph.Width = 11.0;
        closeGlyph.Height = 11.0;
        closeGlyph.Stretch = Stretch.Uniform;
        closeGlyph.Data = Geometry.Parse(
            "M1169 1024l879 -879l-145 -145l-879 879l-879 -879" +
            "l-145 145l879 879l-879 879l145 145l879 -879" +
            "l879 879l145 -145z");
        closeGlyph.Fill = findRequiredThemeBrush(
            CAPTION_FOREGROUND,
            ThemeVariant.Dark);
        closeButton.Content = closeGlyph;

        Window window = new Window();
        window.RequestedThemeVariant = ThemeVariant.Dark;
        window.Content = closeButton;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Point closeButtonCenter = findControlCenter(window, closeButton);
            ContentPresenter closeSurface = closeButton.GetVisualDescendants()
                .OfType<ContentPresenter>()
                .Single(control => control.Name == "PART_ContentPresenter");

            window.MouseMove(
                closeButtonCenter,
                RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(
                closeSurface.Background,
                CAPTION_CLOSE_HOVER_BACKGROUND,
                ThemeVariant.Dark);

            window.MouseDown(
                closeButtonCenter,
                MouseButton.Left,
                RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            assertButtonBrush(
                closeSurface.Background,
                CAPTION_CLOSE_PRESSED_BACKGROUND,
                ThemeVariant.Dark);
            assertButtonBrush(
                closeGlyph.Fill,
                CAPTION_FOREGROUND,
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

    private static void movePointerToControl(
        Window window,
        Control control)
    {
        Point controlCenter = findControlCenter(window, control);
        window.MouseMove(controlCenter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(control.IsPointerOver);
    }

    private static Point findControlCenter(
        Window window,
        Control control)
    {
        Point? controlOriginOrNull = control.TranslatePoint(
            new Point(0.0, 0.0),
            window);
        Assert.NotNull(controlOriginOrNull);
        if (controlOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The control position could not be resolved.");
        }

        return controlOriginOrNull.Value
            + new Vector(control.Bounds.Width / 2.0, control.Bounds.Height / 2.0);
    }

    private static void assertControlsShareVerticalCenter(
        Control root,
        Control firstControl,
        Control secondControl)
    {
        Point? firstOriginOrNull = firstControl.TranslatePoint(
            new Point(0.0, 0.0),
            root);
        Point? secondOriginOrNull = secondControl.TranslatePoint(
            new Point(0.0, 0.0),
            root);
        Assert.NotNull(firstOriginOrNull);
        Assert.NotNull(secondOriginOrNull);
        if (firstOriginOrNull == null || secondOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The button content geometry could not be resolved.");
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

    private static ControlTheme findRequiredControlTheme(
        ControlThemeToken controlThemeToken,
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
            controlThemeToken.Value,
            themeVariant,
            out resourceOrNull);
        Assert.True(
            hasResource,
            "Missing control theme: " + controlThemeToken.Value);

        return Assert.IsType<ControlTheme>(resourceOrNull);
    }

    private readonly record struct ColorToken(string Value);

    private readonly record struct ControlStyleClass(string Value);

    private readonly record struct ControlThemeToken(string Value);
}
