using System;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentIcons.Avalonia;

using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PlanInspectorVisualStateTests
{
    private const double INSPECTOR_HEIGHT = 640.0;
    private const double INSPECTOR_WIDTH = 384.0;
    private const string PRODUCT_FONT_FAMILY_NAME = "Pretendard";
    private const string TERM_PLAN_NAME = "2026-2학기 시간표(5)";

    [AvaloniaFact]
    public void PlanTitleRemainsFullyVisibleAsTheManagementActionAtProductWidths()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.BeginRenamePlanCommand.Execute(null);
            workspace.PlanNameDraft = TERM_PLAN_NAME;
            workspace.ConfirmRenamePlanCommand.Execute(null);

            double[] inspectorWidths = new double[]
            {
                288.0,
                304.0,
            };
            foreach (double inspectorWidth in inspectorWidths)
            {
                PlanInspectorView inspector = new PlanInspectorView();
                inspector.DataContext = workspace;
                Window window = new Window();
                window.Width = inspectorWidth;
                window.Height = INSPECTOR_HEIGHT;
                window.Content = inspector;

                try
                {
                    window.Show();
                    Dispatcher.UIThread.RunJobs();

                    Button managementButton = findRequiredControl<Button>(
                        inspector,
                        "PlanManagementButton");
                    TextBlock planTitle = findRequiredControl<TextBlock>(
                        inspector,
                        "PlanManagementTitle");
                    TextBlock naturalTitle = new TextBlock();
                    naturalTitle.FontFamily = planTitle.FontFamily;
                    naturalTitle.FontSize = planTitle.FontSize;
                    naturalTitle.FontWeight = planTitle.FontWeight;
                    naturalTitle.Text = TERM_PLAN_NAME;
                    naturalTitle.Measure(Size.Infinity);

                    Assert.Equal(TERM_PLAN_NAME, planTitle.Text);
                    Assert.Equal(
                        TextTrimming.CharacterEllipsis,
                        planTitle.TextTrimming);
                    Assert.True(
                        naturalTitle.DesiredSize.Width
                        <= planTitle.Bounds.Width + 0.05,
                        "The product term name did not fit at inspector width "
                            + inspectorWidth + ". Natural text width: "
                            + naturalTitle.DesiredSize.Width
                            + ", arranged title width: "
                            + planTitle.Bounds.Width + ".");
                    Assert.Equal(
                        TERM_PLAN_NAME,
                        AutomationProperties.GetName(managementButton));
                    Assert.Equal(
                        "계획 관리",
                        AutomationProperties.GetHelpText(managementButton));
                    Assert.Equal(
                        2,
                        (int)AutomationProperties.GetHeadingLevel(
                            managementButton));
                    Assert.Equal(
                        "계획 관리",
                        ToolTip.GetTip(managementButton));
                    Assert.Empty(
                        managementButton
                            .GetVisualDescendants()
                            .OfType<FluentIcon>());
                }
                finally
                {
                    window.Close();
                }
            }
        }
    }

    [AvaloniaFact]
    public void EmptyPlanMenuUsesSubduedTextOnlyDisabledTreatmentAcrossThemes()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.ActivePlan = workspace.Plans[1];
            PlanInspectorView inspector = new PlanInspectorView();
            inspector.DataContext = workspace;
            Window window = new Window();
            window.Width = INSPECTOR_WIDTH;
            window.Height = INSPECTOR_HEIGHT;
            window.Content = inspector;

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
                    Dispatcher.UIThread.RunJobs();

                    assertDisabledClearActionVisuals(
                        inspector,
                        themeVariant);
                }
            }
            finally
            {
                window.Close();
            }
        }
    }

    private static void assertDisabledClearActionVisuals(
        PlanInspectorView inspector,
        ThemeVariant themeVariant)
    {
        Button managementButton = findRequiredControl<Button>(
            inspector,
            "PlanManagementButton");
        Flyout managementFlyout = Assert.IsType<Flyout>(
            managementButton.Flyout);
        managementFlyout.ShowAt(managementButton);
        Dispatcher.UIThread.RunJobs();

        try
        {
            StackPanel managementContent = Assert.IsType<StackPanel>(
                managementFlyout.Content);
            Assert.Equal(148.0, managementContent.MinWidth);
            Assert.InRange(managementContent.Bounds.Width, 148.0, 150.0);
            Button[] managementActions = managementContent
                .GetVisualDescendants()
                .OfType<Button>()
                .ToArray();
            Assert.Equal(3, managementActions.Length);
            foreach (Button managementAction in managementActions)
            {
                Assert.Equal(
                    PRODUCT_FONT_FAMILY_NAME,
                    managementAction.FontFamily.Name);
                Assert.Equal(14.0, managementAction.FontSize);
                Assert.Equal(FontWeight.SemiBold, managementAction.FontWeight);
                Assert.Equal(
                    HorizontalAlignment.Stretch,
                    managementAction.HorizontalAlignment);
                Assert.InRange(
                    Math.Abs(
                        managementAction.Bounds.Width
                        - managementContent.Bounds.Width),
                    0.0,
                    0.05);
            }

            Button clearButton = managementActions.Single(
                candidate => candidate.Name == "ClearActivePlanButton");
            ContentPresenter presenter = clearButton
                .GetVisualDescendants()
                .OfType<ContentPresenter>()
                .Single(candidate => candidate.Name == "PART_ContentPresenter");
            StackPanel buttonContent = Assert.IsType<StackPanel>(
                clearButton.Content);
            Assert.Equal(8.0, buttonContent.Spacing);
            Control[] contentChildren = buttonContent.Children
                .OfType<Control>()
                .ToArray();
            FluentIcon clearIcon = Assert.IsType<FluentIcon>(
                contentChildren[0]);
            TextBlock clearLabel = Assert.IsType<TextBlock>(
                contentChildren[1]);

            Assert.False(clearButton.IsEnabled);
            Assert.Equal(1.0, clearButton.Opacity);
            Assert.InRange(presenter.Opacity, 0.719, 0.721);
            assertTransparent(clearButton.Background);
            assertTransparent(clearButton.BorderBrush);
            assertTransparent(presenter.Background);
            assertTransparent(presenter.BorderBrush);
            Assert.Equal(
                getRequiredApplicationColor(
                    "TextTertiaryBrush",
                    themeVariant),
                getRequiredSolidColor(clearButton.Foreground));
            Assert.Equal(2, contentChildren.Length);
            Assert.Equal(18.0, clearIcon.Width);
            Assert.Equal(18.0, clearIcon.Height);
            Assert.Equal(18.0, clearLabel.Height);
            Assert.Equal(18.0, clearLabel.LineHeight);
            assertControlsShareVerticalCenter(
                buttonContent,
                contentChildren[0],
                contentChildren[1]);
            assertControlsShareVerticalCenter(
                clearButton,
                clearButton,
                buttonContent);
        }
        finally
        {
            managementFlyout.Hide();
        }
    }

    private static TControl findRequiredControl<TControl>(
        Control root,
        string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The required plan inspector control was not found: "
                + controlName);
        }

        return controlOrNull;
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
                "The plan management action geometry could not be resolved.");
        }

        double firstCenterY = firstOriginOrNull.Value.Y
            + (firstControl.Bounds.Height / 2.0);
        double secondCenterY = secondOriginOrNull.Value.Y
            + (secondControl.Bounds.Height / 2.0);
        Assert.InRange(
            Math.Abs(firstCenterY - secondCenterY),
            0.0,
            0.05);
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
                "The plan management action brush was not a solid color.");
        }

        return solidBrushOrNull.Color;
    }

    private static void assertTransparent(IBrush? brushOrNull)
    {
        if (brushOrNull == null)
        {
            return;
        }

        Assert.Equal(byte.MinValue, getRequiredSolidColor(brushOrNull).A);
    }
}
