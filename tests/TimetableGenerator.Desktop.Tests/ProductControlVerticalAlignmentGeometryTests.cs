using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentIcons.Avalonia;
using FluentIcons.Common;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ProductControlVerticalAlignmentGeometryTests
{
    private const double APPEARANCE_OPTION_HEIGHT_DIP = 44.0;
    private const double BODY_FONT_SIZE_DIP = 14.0;
    private const double MAXIMUM_LAYOUT_CENTER_DELTA_DIP = 0.05;
    private const double PLAN_TAB_HEIGHT_DIP = 44.0;
    private const double PLAN_TAB_FONT_SIZE_DIP = 16.0;

    private readonly ITestOutputHelper mOutputHelper;

    public ProductControlVerticalAlignmentGeometryTests(
        ITestOutputHelper outputHelper)
    {
        mOutputHelper = outputHelper;
    }

    [AvaloniaFact]
    public void AppearanceOptionsAlignIndicatorAndTextLayoutCentersAcrossStates()
    {
        AppearanceSettingsView view = new AppearanceSettingsView();
        Window window = createWindow(view, 320.0, 280.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            (RadioButton Option, string Text)[] options =
            {
                (
                    findRequiredControl<RadioButton>(
                        view,
                        "SystemThemeOption"),
                    "시스템 설정 사용"),
                (
                    findRequiredControl<RadioButton>(
                        view,
                        "LightThemeOption"),
                    "라이트"),
                (
                    findRequiredControl<RadioButton>(
                        view,
                        "DarkThemeOption"),
                    "다크"),
            };
            List<VerticalCenterComparison> centerComparisons =
                new List<VerticalCenterComparison>();
            List<ControlHeightComparison> heightComparisons =
                new List<ControlHeightComparison>();

            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                foreach ((RadioButton option, string text) in options)
                {
                    foreach (bool isSelected in new bool[] { false, true })
                    {
                        foreach ((RadioButton candidate, _) in options)
                        {
                            candidate.IsChecked = false;
                        }

                        option.IsChecked = isSelected;
                        Dispatcher.UIThread.RunJobs();
                        string stateName = "Appearance option '" + text
                            + "' [theme=" + themeVariant.Key
                            + ", selected=" + isSelected + "]";
                        heightComparisons.Add(new ControlHeightComparison(
                            stateName,
                            APPEARANCE_OPTION_HEIGHT_DIP,
                            option.Bounds.Height));
                        centerComparisons.AddRange(
                            getAppearanceOptionCenterComparisons(
                                option,
                                text,
                                stateName));
                    }
                }
            }

            assertAllGeometryMatches(
                centerComparisons,
                heightComparisons);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleDayOptionsCenterTextLayoutsAcrossStates()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 900.0, 760.0);

        try
        {
            window.Show();
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            ToggleButton[] dayInputs = host.GetVisualDescendants()
                .OfType<ToggleButton>()
                .Where(candidate => candidate.Classes.Contains("day-option"))
                .ToArray();
            Assert.Equal(7, dayInputs.Length);
            List<VerticalCenterComparison> comparisons =
                new List<VerticalCenterComparison>();

            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                foreach (ToggleButton dayInput in dayInputs)
                {
                    PersonalScheduleDayOption dayOption =
                        Assert.IsType<PersonalScheduleDayOption>(
                            dayInput.DataContext);
                    foreach (bool isSelected in new bool[] { false, true })
                    {
                        dayOption.IsSelected = isSelected;
                        Dispatcher.UIThread.RunJobs();
                        TextBlock text = findRequiredTextBlock(
                            dayInput,
                            dayOption.ShortName);
                        comparisons.Add(compareControlAndTextLayoutCenters(
                            "Personal schedule day '" + dayOption.ShortName
                                + "' [theme=" + themeVariant.Key
                                + ", selected=" + isSelected + "]",
                            dayInput,
                            measureTextLayout(text, dayInput)));
                    }
                }
            }

            assertAllLayoutCentersMatch(comparisons);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void CoursePreferenceOptionsCenterTextLayoutsAcrossStates()
    {
        PlannerWorkspaceViewModel workspace = createCourseChoiceWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_200.0, 760.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            workspace.SearchText = "프로그래밍";
            CourseSearchItem course = Assert.Single(workspace.VisibleCourses);
            workspace.AddCourseCommand.Execute(course);
            Dispatcher.UIThread.RunJobs();

            RadioButton[] preferenceInputs = host.GetVisualDescendants()
                .OfType<RadioButton>()
                .Where(
                    candidate => candidate.Classes.Contains(
                        "preference-choice"))
                .Take(3)
                .ToArray();
            Assert.Equal(3, preferenceInputs.Length);
            List<VerticalCenterComparison> comparisons =
                new List<VerticalCenterComparison>();

            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                for (int optionIndex = 0;
                    optionIndex < preferenceInputs.Length;
                    ++optionIndex)
                {
                    RadioButton preferenceInput = preferenceInputs[optionIndex];
                    foreach (bool isSelected in new bool[] { false, true })
                    {
                        int selectedOptionIndex = isSelected
                            ? optionIndex
                            : (optionIndex + 1) % preferenceInputs.Length;
                        preferenceInputs[selectedOptionIndex]
                            .Command?
                            .Execute(null);
                        Dispatcher.UIThread.RunJobs();

                        Assert.Equal(isSelected, preferenceInput.IsChecked);
                        string preferenceText = Assert.IsType<string>(
                            preferenceInput.Content);
                        TextBlock text = findRequiredTextBlock(
                            preferenceInput,
                            preferenceText);
                        comparisons.Add(compareControlAndTextLayoutCenters(
                            "Course preference '" + preferenceText
                                + "' [theme=" + themeVariant.Key
                                + ", selected=" + isSelected + "]",
                            preferenceInput,
                            measureTextLayout(text, preferenceInput)));
                    }
                }
            }

            assertAllLayoutCentersMatch(comparisons);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void ProductButtonsCenterTextLayoutsAcrossThemes()
    {
        Button standardButton = createTextButton("기본 동작", 36.0);
        Button outlineButton = createTextButton("취소", 40.0, "outline");
        Button accentButton = createTextButton("저장", 40.0, "accent");
        Button dangerButton = createTextButton("삭제", 40.0, "danger");
        Button[] buttons =
        {
            standardButton,
            outlineButton,
            accentButton,
            dangerButton,
        };
        StackPanel buttonList = new StackPanel();
        buttonList.Spacing = 8.0;
        foreach (Button button in buttons)
        {
            buttonList.Children.Add(button);
        }

        Window window = createWindow(buttonList, 420.0, 260.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            List<VerticalCenterComparison> comparisons =
                new List<VerticalCenterComparison>();
            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                Dispatcher.UIThread.RunJobs();
                foreach (Button button in buttons)
                {
                    string buttonText = Assert.IsType<string>(button.Content);
                    TextBlock text = findRequiredTextBlock(
                        button,
                        buttonText);
                    comparisons.Add(compareControlAndTextLayoutCenters(
                        "Button '" + buttonText + "' [theme="
                            + themeVariant.Key + "]",
                        button,
                        measureTextLayout(text, button)));
                }
            }

            assertAllLayoutCentersMatch(comparisons);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ProductTextBoxCentersItsTextLayoutAcrossThemes()
    {
        TextBox textBox = new TextBox();
        textBox.Height = 40.0;
        textBox.Text = "과목 검색";
        Window window = createWindow(textBox, 420.0, 120.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            List<VerticalCenterComparison> comparisons =
                new List<VerticalCenterComparison>();
            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                Dispatcher.UIThread.RunJobs();
                TextPresenter text = textBox.GetVisualDescendants()
                    .OfType<TextPresenter>()
                    .Single(candidate => candidate.Text == "과목 검색");
                comparisons.Add(compareControlAndTextLayoutCenters(
                    "TextBox '과목 검색' [theme=" + themeVariant.Key + "]",
                    textBox,
                    measureTextLayout(text, textBox)));
            }

            assertAllLayoutCentersMatch(comparisons);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CourseBrowserInputsCenterTextLayoutsAcrossThemes()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_200.0, 760.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            CourseBrowserView courseBrowser = host.GetVisualDescendants()
                .OfType<CourseBrowserView>()
                .Single();
            TextBox searchInput = findRequiredControl<TextBox>(
                courseBrowser,
                "CourseSearchBox");
            ComboBox[] selectionInputs = courseBrowser
                .GetVisualDescendants()
                .OfType<ComboBox>()
                .Where(candidate => candidate.IsEffectivelyVisible)
                .ToArray();
            Assert.Equal(3, selectionInputs.Length);
            List<VerticalCenterComparison> comparisons =
                new List<VerticalCenterComparison>();

            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                Dispatcher.UIThread.RunJobs();

                TextBlock searchPlaceholder = searchInput
                    .GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(candidate => candidate.Name == "PART_Placeholder");
                Assert.Equal(
                    "과목명, 코드, 교수 검색",
                    searchPlaceholder.Text);
                comparisons.Add(compareControlAndTextLayoutCenters(
                    "Course search placeholder [theme="
                        + themeVariant.Key + "]",
                    searchInput,
                    measureTextLayout(searchPlaceholder, searchInput)));

                foreach (ComboBox selectionInput in selectionInputs)
                {
                    TextBlock selectedText = selectionInput
                        .GetVisualDescendants()
                        .OfType<TextBlock>()
                        .Single(
                            candidate => ReferenceEquals(
                                candidate.DataContext,
                                selectionInput.SelectedItem));
                    comparisons.Add(compareControlAndTextLayoutCenters(
                        "Course selection '" + selectedText.Text
                            + "' [theme=" + themeVariant.Key + "]",
                        selectionInput,
                        measureTextLayout(selectedText, selectionInput)));
                }
            }

            assertAllLayoutCentersMatch(comparisons);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void ProductTimePickerSegmentsCenterTextLayoutsAcrossThemes()
    {
        ProductTimePicker timePicker = new ProductTimePicker();
        timePicker.SelectedTimeOrNull = new ScheduleTime(13, 30);
        Window window = createWindow(timePicker, 420.0, 120.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ComboBox[] segments = timePicker.GetVisualDescendants()
                .OfType<ComboBox>()
                .Where(candidate => candidate.Classes.Contains("time-segment"))
                .ToArray();
            Assert.Equal(3, segments.Length);
            List<VerticalCenterComparison> comparisons =
                new List<VerticalCenterComparison>();

            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                Dispatcher.UIThread.RunJobs();
                foreach (ComboBox segment in segments)
                {
                    TextBlock text = segment.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .Single(
                            candidate => ReferenceEquals(
                                candidate.DataContext,
                                segment.SelectedItem));
                    comparisons.Add(compareControlAndTextLayoutCenters(
                        "ProductTimePicker segment '" + segment.Name
                            + "' [theme=" + themeVariant.Key + "]",
                        segment,
                        measureTextLayout(text, segment)));
                }
            }

            assertAllLayoutCentersMatch(comparisons);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PlanTabsCenterTextAndCloseIconsAcrossStates()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_000.0, 760.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            TabStrip planTabs = host.GetVisualDescendants()
                .OfType<TabStrip>()
                .Single(candidate => candidate.Classes.Contains("plan-tabs"));
            TabStripItem[] planTabItems = planTabs.GetVisualDescendants()
                .OfType<TabStripItem>()
                .ToArray();
            Assert.Equal(workspace.Plans.Count, planTabItems.Length);
            List<VerticalCenterComparison> centerComparisons =
                new List<VerticalCenterComparison>();
            List<ControlHeightComparison> heightComparisons =
                new List<ControlHeightComparison>();

            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                heightComparisons.Add(new ControlHeightComparison(
                    "Plan TabStrip [theme=" + themeVariant.Key + "]",
                    PLAN_TAB_HEIGHT_DIP,
                    planTabs.Bounds.Height));
                foreach (PlanTabItem selectedPlan in workspace.Plans)
                {
                    workspace.ActivePlan = selectedPlan;
                    Dispatcher.UIThread.RunJobs();
                    foreach (TabStripItem planTabItem in planTabItems)
                    {
                        PlanTabItem plan = Assert.IsType<PlanTabItem>(
                            planTabItem.DataContext);
                        bool isSelected = ReferenceEquals(plan, selectedPlan);
                        TextBlock text = findRequiredTextBlock(
                            planTabItem,
                            plan.DisplayName);
                        Button closeButton = planTabItem.GetVisualDescendants()
                            .OfType<Button>()
                            .Single();
                        FluentIcon closeIcon = closeButton
                            .GetVisualDescendants()
                            .OfType<FluentIcon>()
                            .Single();
                        string stateName = " [theme=" + themeVariant.Key
                            + ", selected=" + isSelected + "]";

                        heightComparisons.Add(new ControlHeightComparison(
                            "Plan TabStripItem '" + plan.DisplayName
                                + "'" + stateName,
                            PLAN_TAB_HEIGHT_DIP,
                            planTabItem.Bounds.Height));
                        centerComparisons.Add(compareCenters(
                            "Plan TabStripItem '" + plan.DisplayName
                                + "' versus TabStrip" + stateName,
                            measureArrangedBounds(planTabs, planTabs),
                            measureArrangedBounds(planTabItem, planTabs)));
                        centerComparisons.Add(
                            compareControlAndTextLayoutCenters(
                                "Plan tab '" + plan.DisplayName + "'"
                                    + stateName,
                                planTabItem,
                                measureTextLayout(
                                    text,
                                    planTabItem,
                                    PLAN_TAB_FONT_SIZE_DIP)));
                        centerComparisons.Add(compareCenters(
                            "Plan tab close button versus icon '"
                                + plan.DisplayName + "'" + stateName,
                            measureArrangedBounds(closeButton, closeButton),
                            measureArrangedBounds(closeIcon, closeButton)));
                        centerComparisons.Add(compareCenters(
                            "Plan tab item versus close button '"
                                + plan.DisplayName + "'" + stateName,
                            measureArrangedBounds(planTabItem, planTabItem),
                            measureArrangedBounds(closeButton, planTabItem)));
                    }
                }
            }

            assertAllGeometryMatches(
                centerComparisons,
                heightComparisons);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PlanTabContextMenuCentersIconAndTextAcrossThemes()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_000.0, 760.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            TabStripItem planTabItem = host.GetVisualDescendants()
                .OfType<TabStripItem>()
                .First();
            StackPanel contextMenuOwner = planTabItem
                .GetVisualDescendants()
                .OfType<StackPanel>()
                .Single(candidate => candidate.ContextMenu != null);
            ContextMenu contextMenu = Assert.IsType<ContextMenu>(
                contextMenuOwner.ContextMenu);
            Assert.Equal(new Thickness(4.0), contextMenu.Padding);
            MenuItem[] menuItems = contextMenu.Items
                .OfType<MenuItem>()
                .ToArray();
            Assert.Equal(2, menuItems.Length);
            List<VerticalCenterComparison> comparisons =
                new List<VerticalCenterComparison>();

            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                contextMenu.Open(contextMenuOwner);
                Dispatcher.UIThread.RunJobs();

                Assert.InRange(contextMenu.Bounds.Width, 158.0, 170.0);
                Assert.InRange(
                    Math.Abs(menuItems[0].Bounds.Width - menuItems[1].Bounds.Width),
                    0.0,
                    0.05);
                foreach (MenuItem menuItem in menuItems)
                {
                    Assert.Equal("Pretendard", menuItem.FontFamily.Name);
                    Assert.Equal(BODY_FONT_SIZE_DIP, menuItem.FontSize);
                    Assert.Equal(FontWeight.SemiBold, menuItem.FontWeight);
                    string headerText = Assert.IsType<string>(menuItem.Header);
                    TextBlock header = findRequiredTextBlock(
                        menuItem,
                        headerText);
                    ContentControl iconPresenter = menuItem
                        .GetVisualDescendants()
                        .OfType<ContentControl>()
                        .Single(
                            candidate => candidate.Name == "PART_IconPresenter");
                    FluentIcon icon = iconPresenter
                        .GetVisualDescendants()
                        .OfType<FluentIcon>()
                        .Single();
                    string stateName = "Plan tab context menu '"
                        + headerText + "' [theme=" + themeVariant.Key + "]";

                    Assert.InRange(menuItem.Bounds.Width, 148.0, 160.0);
                    Assert.Equal(18.0, header.Height);
                    Assert.Equal(18.0, header.LineHeight);
                    Assert.Equal(18.0, icon.Width);
                    Assert.Equal(18.0, icon.Height);
                    comparisons.Add(compareControlAndTextLayoutCenters(
                        stateName,
                        menuItem,
                        measureTextLayout(header, menuItem)));
                    comparisons.Add(compareCenters(
                        stateName + " item versus icon slot",
                        measureArrangedBounds(menuItem, menuItem),
                        measureArrangedBounds(iconPresenter, menuItem)));
                    comparisons.Add(compareCenters(
                        stateName + " icon slot versus icon",
                        measureArrangedBounds(iconPresenter, iconPresenter),
                        measureArrangedBounds(icon, iconPresenter)));
                }

                contextMenu.Close();
                Dispatcher.UIThread.RunJobs();
            }

            assertAllLayoutCentersMatch(comparisons);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void CompoundButtonAlignsIconBoxAndTextLayoutCentersAcrossThemes()
    {
        FluentIcon iconBox = new FluentIcon();
        iconBox.Width = 16.0;
        iconBox.Height = 16.0;
        iconBox.VerticalAlignment = VerticalAlignment.Center;
        iconBox.Icon = Icon.Calendar;
        iconBox.IconVariant = IconVariant.Regular;
        iconBox.FontSize = 16.0;

        TextBlock label = new TextBlock();
        label.Text = "내 계획 열기";
        label.VerticalAlignment = VerticalAlignment.Center;

        StackPanel buttonContent = new StackPanel();
        buttonContent.Classes.Add("button-content");
        buttonContent.Children.Add(iconBox);
        buttonContent.Children.Add(label);

        Button button = new Button();
        button.Height = 36.0;
        button.Content = buttonContent;
        Window window = createWindow(button, 320.0, 120.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            List<VerticalCenterComparison> comparisons =
                new List<VerticalCenterComparison>();
            foreach (ThemeVariant themeVariant in getProductThemeVariants())
            {
                window.RequestedThemeVariant = themeVariant;
                Dispatcher.UIThread.RunJobs();
                VerticalCenterMeasurement iconMeasurement =
                    measureArrangedBounds(iconBox, button);
                VerticalCenterMeasurement textMeasurement =
                    measureTextLayout(label, button);
                comparisons.Add(compareCenters(
                    "Button icon versus '내 계획 열기' [theme="
                        + themeVariant.Key + "]",
                    iconMeasurement,
                    textMeasurement));
                comparisons.Add(compareControlAndTextLayoutCenters(
                    "Compound Button '내 계획 열기' [theme="
                        + themeVariant.Key + "]",
                    button,
                    textMeasurement));
            }

            assertAllLayoutCentersMatch(comparisons);
        }
        finally
        {
            window.Close();
        }
    }

    private static PlannerWorkspaceViewModel createCourseChoiceWorkspace()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace(
                CatalogProjectionTestFixture
                    .CreateDocumentWithScheduledAlternativeCourse());
        workspace.ActivePlan = workspace.Plans[1];
        return workspace;
    }

    private static Button createTextButton(
        string content,
        double height)
    {
        Button button = new Button();
        button.Height = height;
        button.Content = content;
        return button;
    }

    private static Button createTextButton(
        string content,
        double height,
        string className)
    {
        Button button = createTextButton(content, height);
        button.Classes.Add(className);
        return button;
    }

    private static IReadOnlyList<VerticalCenterComparison>
        getAppearanceOptionCenterComparisons(
        RadioButton option,
        string expectedText,
        string stateName)
    {
        Visual indicator = option.GetVisualDescendants()
            .Single(candidate => candidate.Name == "OuterEllipse");
        TextBlock text = findRequiredTextBlock(option, expectedText);
        VerticalCenterMeasurement optionMeasurement =
            measureArrangedBounds(option, option);
        VerticalCenterMeasurement indicatorMeasurement =
            measureArrangedBounds(indicator, option);
        VerticalCenterMeasurement textMeasurement =
            measureTextLayout(text, option);
        return new VerticalCenterComparison[]
        {
            compareCenters(
                stateName + " option versus indicator",
                optionMeasurement,
                indicatorMeasurement),
            compareCenters(
                stateName + " option versus text layout",
                optionMeasurement,
                textMeasurement),
            compareCenters(
                stateName + " indicator versus text layout",
                indicatorMeasurement,
                textMeasurement),
        };
    }

    private static VerticalCenterComparison compareControlAndTextLayoutCenters(
        string controlName,
        Control control,
        VerticalCenterMeasurement textMeasurement)
    {
        return compareCenters(
            controlName + " control versus text layout",
            measureArrangedBounds(control, control),
            textMeasurement);
    }

    private static VerticalCenterComparison compareCenters(
        string measurementName,
        VerticalCenterMeasurement referenceMeasurement,
        VerticalCenterMeasurement comparedMeasurement)
    {
        return new VerticalCenterComparison(
            measurementName,
            referenceMeasurement,
            comparedMeasurement);
    }

    private void assertAllLayoutCentersMatch(
        IReadOnlyList<VerticalCenterComparison> comparisons)
    {
        string comparisonReport = string.Join(
            Environment.NewLine,
            comparisons.Select(formatComparison));
        mOutputHelper.WriteLine(comparisonReport);
        Assert.True(
            comparisons.All(comparison => comparison.IsWithinTolerance),
            comparisonReport);
    }

    private void assertAllGeometryMatches(
        IReadOnlyList<VerticalCenterComparison> centerComparisons,
        IReadOnlyList<ControlHeightComparison> heightComparisons)
    {
        string centerReport = string.Join(
            Environment.NewLine,
            centerComparisons.Select(formatComparison));
        string heightReport = string.Join(
            Environment.NewLine,
            heightComparisons.Select(formatHeightComparison));
        string geometryReport = heightReport
            + Environment.NewLine
            + centerReport;
        mOutputHelper.WriteLine(geometryReport);
        Assert.True(
            centerComparisons.All(
                comparison => comparison.IsWithinTolerance)
            && heightComparisons.All(
                comparison => comparison.IsWithinTolerance),
            geometryReport);
    }

    private static string formatComparison(
        VerticalCenterComparison comparison)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{comparison.MeasurementName}: layout center delta="
            + $"{comparison.SignedLayoutCenterDelta:F3} DIP "
            + $"(absolute={comparison.AbsoluteLayoutCenterDelta:F3} DIP, "
            + $"allowed={MAXIMUM_LAYOUT_CENTER_DELTA_DIP:F3} DIP); "
            + $"ink center delta={comparison.SignedInkCenterDelta:F3} DIP; "
            + $"reference top={comparison.Reference.Top:F3}, "
            + $"height={comparison.Reference.Height:F3}, "
            + $"center={comparison.Reference.CenterY:F3}; "
            + $"text top={comparison.Compared.Top:F3}, "
            + $"height={comparison.Compared.Height:F3}, "
            + $"center={comparison.Compared.CenterY:F3}, "
            + $"ink center={comparison.Compared.InkCenterY:F3}.");
    }

    private static string formatHeightComparison(
        ControlHeightComparison comparison)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{comparison.MeasurementName}: height delta="
            + $"{comparison.SignedHeightDelta:F3} DIP "
            + $"(absolute={comparison.AbsoluteHeightDelta:F3} DIP, "
            + $"allowed={MAXIMUM_LAYOUT_CENTER_DELTA_DIP:F3} DIP); "
            + $"expected={comparison.ExpectedHeight:F3}, "
            + $"actual={comparison.ActualHeight:F3}.");
    }

    private static VerticalCenterMeasurement measureArrangedBounds(
        Visual visual,
        Visual relativeTo)
    {
        Point origin = findRequiredOrigin(visual, relativeTo);
        return new VerticalCenterMeasurement(
            origin.Y,
            visual.Bounds.Height,
            origin.Y + (visual.Bounds.Height / 2.0));
    }

    private static VerticalCenterMeasurement measureTextLayout(
        TextBlock text,
        Visual relativeTo,
        double expectedFontSize = BODY_FONT_SIZE_DIP)
    {
        Assert.Equal(expectedFontSize, text.FontSize);
        Point origin = findRequiredOrigin(text, relativeTo);
        return measureTextLayout(text.TextLayout, origin.Y + text.Padding.Top);
    }

    private static VerticalCenterMeasurement measureTextLayout(
        TextPresenter text,
        Visual relativeTo)
    {
        Assert.Equal(BODY_FONT_SIZE_DIP, text.FontSize);
        Point origin = findRequiredOrigin(text, relativeTo);
        return measureTextLayout(text.TextLayout, origin.Y);
    }

    private static VerticalCenterMeasurement measureTextLayout(
        TextLayout textLayout,
        double layoutTop)
    {
        double inkBottom = layoutTop
            + textLayout.Height
            + textLayout.OverhangAfter;
        double inkTop = inkBottom - textLayout.Extent;
        double inkCenterY = inkTop + (textLayout.Extent / 2.0);

        return new VerticalCenterMeasurement(
            layoutTop,
            textLayout.Height,
            inkCenterY);
    }

    private static Point findRequiredOrigin(Visual visual, Visual relativeTo)
    {
        Point? originOrNull = visual.TranslatePoint(
            new Point(0.0, 0.0),
            relativeTo);
        Assert.NotNull(originOrNull);
        if (originOrNull == null)
        {
            throw new InvalidOperationException(
                "The control was not attached to the requested visual root.");
        }

        return originOrNull.Value;
    }

    private static TextBlock findRequiredTextBlock(
        Control root,
        string expectedText)
    {
        TextBlock? textOrNull = root.GetVisualDescendants()
            .OfType<TextBlock>()
            .SingleOrDefault(candidate => candidate.Text == expectedText);
        Assert.NotNull(textOrNull);
        if (textOrNull == null)
        {
            throw new InvalidOperationException(
                "The rendered text block could not be resolved: "
                + expectedText);
        }

        return textOrNull;
    }

    private static TControl findRequiredControl<TControl>(
        Control root,
        string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The control could not be resolved: " + controlName);
        }

        return controlOrNull;
    }

    private static IReadOnlyList<ThemeVariant> getProductThemeVariants()
    {
        return new ThemeVariant[]
        {
            ThemeVariant.Light,
            ThemeVariant.Dark,
        };
    }

    private static Window createWindow(
        Control content,
        double width,
        double height)
    {
        Window window = new Window();
        window.Width = width;
        window.Height = height;
        window.Content = content;
        return window;
    }

    private readonly record struct VerticalCenterComparison(
        string MeasurementName,
        VerticalCenterMeasurement Reference,
        VerticalCenterMeasurement Compared)
    {
        public double SignedLayoutCenterDelta =>
            Compared.CenterY - Reference.CenterY;

        public double AbsoluteLayoutCenterDelta =>
            Math.Abs(SignedLayoutCenterDelta);

        public double SignedInkCenterDelta =>
            Compared.InkCenterY - Reference.CenterY;

        public bool IsWithinTolerance =>
            AbsoluteLayoutCenterDelta <= MAXIMUM_LAYOUT_CENTER_DELTA_DIP;
    }

    private readonly record struct ControlHeightComparison(
        string MeasurementName,
        double ExpectedHeight,
        double ActualHeight)
    {
        public double SignedHeightDelta => ActualHeight - ExpectedHeight;

        public double AbsoluteHeightDelta => Math.Abs(SignedHeightDelta);

        public bool IsWithinTolerance =>
            AbsoluteHeightDelta <= MAXIMUM_LAYOUT_CENTER_DELTA_DIP;
    }

    private readonly record struct VerticalCenterMeasurement(
        double Top,
        double Height,
        double InkCenterY)
    {
        public double CenterY => Top + (Height / 2.0);
    }
}
