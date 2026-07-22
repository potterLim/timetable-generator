using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Tests.Presentation.Appearance;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ProductModalAndTabVisualStateTests
{
    private static readonly ColorToken CONTROL_BORDER = new ColorToken("ControlBorderBrush");

    private static readonly ColorToken CONTROL_SURFACE = new ColorToken("ControlSurfaceBrush");

    private static readonly ColorToken SELECTION_INDICATOR = new ColorToken("SelectionIndicatorBrush");

    private static readonly ColorToken TEXT_PRIMARY = new ColorToken("TextPrimaryBrush");

    [AvaloniaFact]
    public void ModalBackgroundKeepsRoleSurfacesWhileRemainingDisabled()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button? workspaceRetryButtonOrNull = host.FindControl<Button>("WorkspaceRetryAutosaveButton");
            Button? emptyWorkspaceRetryButtonOrNull = host.FindControl<Button>("EmptyWorkspaceRetryAutosaveButton");
            Assert.NotNull(workspaceRetryButtonOrNull);
            Assert.NotNull(emptyWorkspaceRetryButtonOrNull);
            Assert.Contains("subtle", workspaceRetryButtonOrNull.Classes);
            Assert.Contains("subtle", emptyWorkspaceRetryButtonOrNull.Classes);

            ThemeVariant[] themeVariants =
            {
                ThemeVariant.Light,
                ThemeVariant.Dark,
            };
            foreach (ThemeVariant themeVariant in themeVariants)
            {
                window.RequestedThemeVariant = themeVariant;
                workspace.BeginAddPersonalScheduleCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();

                Grid workspaceSurface = findRequiredControl<Grid>(host, "WorkspaceSurface");
                Button iconButton = findRequiredControl<Button>(host, "AddPlanButton");
                TextBox searchBox = findRequiredControl<TextBox>(host, "CourseSearchBox");
                ComboBox departmentFilter = findRequiredControl<ComboBox>(host, "DepartmentFilter");
                Button exportButton = findRequiredControl<Button>(host, "ExportScheduleButton");
                Button outlineButton = findRequiredControl<Button>(host, "WorkspaceAddPersonalScheduleButton");
                TabStrip planTabs = host.GetVisualDescendants()
                    .OfType<TabStrip>()
                    .Single(candidate => candidate.Classes.Contains("plan-tabs"));
                Button[] planTabCloseButtons = planTabs.GetVisualDescendants()
                    .OfType<Button>()
                    .Where(static button => button.IsVisible)
                    .ToArray();

                Assert.False(workspaceSurface.IsEnabled);
                Assert.False(iconButton.IsEffectivelyEnabled);
                Assert.False(searchBox.IsEffectivelyEnabled);
                Assert.False(departmentFilter.IsEffectivelyEnabled);
                Assert.False(outlineButton.IsEffectivelyEnabled);
                Assert.NotEmpty(planTabCloseButtons);

                ContentPresenter iconPresenter = findRequiredTemplateControl<ContentPresenter>(
                    iconButton,
                    "PART_ContentPresenter");
                assertTransparent(iconPresenter.Background);
                assertTransparent(iconPresenter.BorderBrush);
                Assert.Equal(1.0, iconButton.Opacity);
                Assert.Equal(1.0, iconPresenter.Opacity);

                foreach (Button planTabCloseButton in planTabCloseButtons)
                {
                    Assert.False(planTabCloseButton.IsEffectivelyEnabled);
                    Assert.Equal(1.0, planTabCloseButton.Opacity);
                    ContentPresenter planTabClosePresenter =
                        findRequiredTemplateControl<ContentPresenter>(
                            planTabCloseButton,
                            "PART_ContentPresenter");
                    assertTransparent(planTabClosePresenter.Background);
                    assertTransparent(planTabClosePresenter.BorderBrush);
                }

                ContentPresenter outlinePresenter =
                    findRequiredTemplateControl<ContentPresenter>(
                        outlineButton,
                        "PART_ContentPresenter");
                Assert.Equal(1.0, outlineButton.Opacity);
                Assert.Equal(1.0, outlinePresenter.Opacity);
                assertBrushUsesToken(outlinePresenter.Background, CONTROL_SURFACE, themeVariant);
                assertBrushUsesToken(outlinePresenter.BorderBrush, CONTROL_BORDER, themeVariant);
                assertBrushUsesToken(outlineButton.Foreground, TEXT_PRIMARY, themeVariant);

                Border textBoxBorder = findRequiredTemplateControl<Border>(searchBox, "PART_BorderElement");
                Assert.Equal(1.0, searchBox.Opacity);
                assertBrushUsesToken(textBoxBorder.Background, CONTROL_SURFACE, themeVariant);
                assertBrushUsesToken(textBoxBorder.BorderBrush, CONTROL_BORDER, themeVariant);

                Border comboBoxBorder = findRequiredTemplateControl<Border>(departmentFilter, "Background");
                Assert.Equal(1.0, departmentFilter.Opacity);
                assertBrushUsesToken(comboBoxBorder.Background, CONTROL_SURFACE, themeVariant);
                assertBrushUsesToken(comboBoxBorder.BorderBrush, CONTROL_BORDER, themeVariant);

                ContentPresenter exportPresenter =
                    findRequiredTemplateControl<ContentPresenter>(
                        exportButton,
                        "PART_ContentPresenter");
                Assert.Equal(1.0, exportPresenter.Opacity);

                workspace.CancelPersonalScheduleEditCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void AppearanceButtonKeepsAQuietTransparentDisabledStateDuringModal()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductShellViewModel shell = PlannerWorkspaceTestFactory.CreateShell(workspace);
        MainWindow window = new MainWindow(shell, ProductAppearanceTestFactory.CreateViewModel());

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button appearanceButton = findRequiredControl<Button>(window, "AppearanceButton");
            ThemeVariant[] themeVariants =
            {
                ThemeVariant.Light,
                ThemeVariant.Dark,
            };
            foreach (ThemeVariant themeVariant in themeVariants)
            {
                window.RequestedThemeVariant = themeVariant;
                workspace.BeginAddPersonalScheduleCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();

                Assert.False(appearanceButton.IsEffectivelyEnabled);
                Assert.Equal(0.62, appearanceButton.Opacity);
                ContentPresenter presenter =
                    findRequiredTemplateControl<ContentPresenter>(
                        appearanceButton,
                        "PART_ContentPresenter");
                assertTransparent(presenter.Background);
                assertTransparent(presenter.BorderBrush);

                workspace.CancelPersonalScheduleEditCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
            }
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void DisabledIconUsesAQuietTransparentSurface()
    {
        Button iconButton = new Button();
        iconButton.Classes.Add("icon");
        iconButton.IsEnabled = false;
        Window window = createWindow(iconButton);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ContentPresenter presenter = findRequiredTemplateControl<ContentPresenter>(
                iconButton,
                "PART_ContentPresenter");
            Assert.False(iconButton.IsEffectivelyEnabled);
            Assert.Equal(0.62, iconButton.Opacity);
            assertTransparent(presenter.Background);
            assertTransparent(presenter.BorderBrush);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PlanTabsRenderOneFullWidthProductSelectionIndicator()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host);

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

                TabStrip planTabs = host.GetVisualDescendants()
                    .OfType<TabStrip>()
                    .Single(candidate => candidate.Classes.Contains("plan-tabs"));
                TabStripItem[] planTabItems = planTabs.GetVisualDescendants().OfType<TabStripItem>().ToArray();
                Border[] selectionPipes = planTabItems
                    .Select(
                        tab => findRequiredTemplateControl<Border>(
                            tab,
                            "PART_SelectedPipe"))
                    .ToArray();

                TabStripItem selectedTab = Assert.Single(
                    planTabItems,
                    static tab => tab.IsSelected);
                Border selectedPipe = findRequiredTemplateControl<Border>(selectedTab, "PART_SelectedPipe");

                Assert.Equal(new Thickness(0.0, 0.0, 0.0, 2.0), selectedTab.BorderThickness);
                Assert.All(
                    selectionPipes,
                    static pipe => Assert.False(pipe.IsVisible));
                Assert.False(selectedPipe.IsVisible);
                assertBrushUsesToken(selectedTab.BorderBrush, SELECTION_INDICATOR, themeVariant);
            }
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static void assertBrushUsesToken(
        IBrush? actualBrushOrNull,
        ColorToken expectedToken,
        ThemeVariant themeVariant)
    {
        ISolidColorBrush? actualSolidBrushOrNull = actualBrushOrNull as ISolidColorBrush;
        Assert.NotNull(actualSolidBrushOrNull);
        if (actualSolidBrushOrNull == null)
        {
            throw new InvalidOperationException("The rendered brush was not a solid color brush.");
        }

        SolidColorBrush expectedBrush = findRequiredBrush(expectedToken, themeVariant);
        Assert.Equal(expectedBrush.Color, actualSolidBrushOrNull.Color);
    }

    private static void assertTransparent(IBrush? brushOrNull)
    {
        ISolidColorBrush? solidBrushOrNull = brushOrNull as ISolidColorBrush;
        Assert.NotNull(solidBrushOrNull);
        if (solidBrushOrNull == null)
        {
            throw new InvalidOperationException("The rendered brush was not a solid color brush.");
        }

        Assert.Equal(byte.MinValue, solidBrushOrNull.Color.A);
    }

    private static TControl findRequiredControl<TControl>(Control root, string name)
        where TControl : Control
    {
        TControl? controlOrNull = root.GetVisualDescendants()
            .OfType<TControl>()
            .SingleOrDefault(candidate => candidate.Name == name);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("The required control was not found: " + name);
        }

        return controlOrNull;
    }

    private static TControl findRequiredTemplateControl<TControl>(Control root, string name)
        where TControl : Control
    {
        TControl? controlOrNull = root.GetVisualDescendants()
            .OfType<TControl>()
            .SingleOrDefault(candidate => candidate.Name == name);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("The required template control was not found: " + name);
        }

        return controlOrNull;
    }

    private static SolidColorBrush findRequiredBrush(ColorToken colorToken, ThemeVariant themeVariant)
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
        Assert.True(hasResource);

        SolidColorBrush? brushOrNull = resourceOrNull as SolidColorBrush;
        Assert.NotNull(brushOrNull);
        if (brushOrNull == null)
        {
            throw new InvalidOperationException(
                "The product color token was not a solid color brush: " +
                    colorToken.Value);
        }

        return brushOrNull;
    }

    private static Window createWindow(Control content)
    {
        Window window = new Window();
        window.Width = 1_600.0;
        window.Height = 960.0;
        window.Content = content;
        return window;
    }

    private readonly record struct ColorToken(string Value);
}
