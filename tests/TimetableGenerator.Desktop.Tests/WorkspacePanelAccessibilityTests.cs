using System;
using System.Linq;
using System.Windows.Input;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class WorkspacePanelAccessibilityTests
{
    private const double INSPECTOR_WIDTH = 384.0;
    private const double MINIMUM_PRODUCT_WINDOW_HEIGHT = 640.0;
    private const double PRODUCT_NAVIGATION_HEIGHT = 100.0;
    private const double MINIMUM_WORKSPACE_HEIGHT =
        MINIMUM_PRODUCT_WINDOW_HEIGHT - PRODUCT_NAVIGATION_HEIGHT;

    [AvaloniaFact]
    public void InspectorContentRemainsReachableAtMinimumWindowHeight()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.SearchText = "세미나";
        CourseSearchItem unscheduledCourse = Assert.Single(workspace.VisibleCourses);
        workspace.AddCourseCommand.Execute(unscheduledCourse);

        PlanInspectorView inspector = new PlanInspectorView();
        inspector.DataContext = workspace;
        Window window = createPanelWindow(inspector);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ScrollViewer scrollViewer = findRequiredControl<ScrollViewer>(
                inspector,
                "PlanInspectorScrollViewer");
            Border recommendationPolicyCard = findRequiredControl<Border>(
                inspector,
                "RecommendationPolicyCard");

            Assert.True(scrollViewer.Extent.Height > scrollViewer.Viewport.Height);

            scrollViewer.ScrollToEnd();
            Dispatcher.UIThread.RunJobs();

            Point? policyCardTopLeftOrNull = recommendationPolicyCard.TranslatePoint(
                new Point(0.0, 0.0),
                scrollViewer);
            Assert.NotNull(policyCardTopLeftOrNull);
            if (policyCardTopLeftOrNull == null)
            {
                throw new InvalidOperationException(
                    "The recommendation policy card was not attached to the inspector viewport.");
            }

            Point policyCardTopLeft = policyCardTopLeftOrNull.Value;
            double policyCardBottom =
                policyCardTopLeft.Y + recommendationPolicyCard.Bounds.Height;
            Assert.True(policyCardTopLeft.Y >= 0.0);
            Assert.True(policyCardBottom <= scrollViewer.Viewport.Height + 1.0);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void WorkspaceListsSkipDecorativeCardFocusAndRetainActionFocus()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        CourseBrowserView courseBrowser = new CourseBrowserView();
        courseBrowser.DataContext = workspace;
        PlanInspectorView inspector = new PlanInspectorView();
        inspector.DataContext = workspace;

        Grid panels = new Grid();
        panels.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)));
        panels.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.0, GridUnitType.Star)));
        Grid.SetColumn(inspector, 1);
        panels.Children.Add(courseBrowser);
        panels.Children.Add(inspector);

        Window window = createPanelWindow(panels);
        window.Width = 768.0;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ListBox courseResults = findRequiredControl<ListBox>(
                courseBrowser,
                "CourseResultsList");
            ListBox scheduledCourses = findRequiredControl<ListBox>(
                inspector,
                "ScheduledCoursesList");

            assertListDelegatesFocusToCommand(
                courseResults,
                workspace.AddCourseCommand);
            assertListDelegatesFocusToCommand(
                scheduledCourses,
                workspace.RemoveCourseChoiceGroupCommand);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void CourseBrowserInputsAlignAndMultiOfferingCardsStayConcise()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.SearchText = "프로그래밍";
            CourseBrowserView courseBrowser = new CourseBrowserView();
            courseBrowser.DataContext = workspace;
            Window window = createPanelWindow(courseBrowser);

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                TextBox searchBox = findRequiredControl<TextBox>(
                    courseBrowser,
                    "CourseSearchBox");
                Assert.Equal(
                    new Thickness(36.0, 0.0, 12.0, 0.0),
                    searchBox.Padding);
                Assert.Equal(
                    VerticalAlignment.Center,
                    searchBox.VerticalContentAlignment);

                ComboBox[] selectors = courseBrowser.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .ToArray();
                Assert.NotEmpty(selectors);
                Assert.All(
                    selectors,
                    selector => Assert.True(selector.MinHeight >= 40.0));
                Assert.All(
                    selectors,
                    selector => Assert.Equal(
                        VerticalAlignment.Center,
                        selector.VerticalContentAlignment));

                TextBlock[] visibleTexts = courseBrowser.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(candidate => candidate.IsVisible)
                    .ToArray();
                Assert.DoesNotContain(
                    visibleTexts,
                    candidate => candidate.Text != null
                        && candidate.Text.Contains(
                            "선호할 분반",
                            StringComparison.Ordinal));
                Assert.DoesNotContain(
                    visibleTexts,
                    candidate => candidate.Text != null
                        && candidate.Text.Contains(
                            "분반별 강의실",
                            StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void AddedCourseKeepsItsSurfaceAcrossListAndPointerStates()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            CourseBrowserView courseBrowser = new CourseBrowserView();
            courseBrowser.DataContext = workspace;
            Window window = createPanelWindow(courseBrowser);

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                ListBox courseResults = findRequiredControl<ListBox>(
                    courseBrowser,
                    "CourseResultsList");
                Control? itemContainerOrNull = courseResults.ContainerFromIndex(0);
                Assert.NotNull(itemContainerOrNull);
                if (itemContainerOrNull == null)
                {
                    throw new InvalidOperationException(
                        "The first course result was not realized.");
                }

                Border courseCard = itemContainerOrNull
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Single(candidate => candidate.Classes.Contains("course-item"));
                ContentPresenter itemPresenter = itemContainerOrNull
                    .GetVisualChildren()
                    .OfType<ContentPresenter>()
                    .Single(candidate => candidate.Name == "PART_ContentPresenter");
                CourseSearchItem course = workspace.VisibleCourses[0];

                workspace.AddCourseCommand.Execute(course);
                courseResults.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();

                Assert.Contains("added", courseCard.Classes);
                Color restingColor = getRequiredSolidColor(courseCard.Background);
                assertTransparent(itemPresenter.Background);

                Point? cardOriginOrNull = courseCard.TranslatePoint(
                    new Point(0.0, 0.0),
                    window);
                Assert.NotNull(cardOriginOrNull);
                if (cardOriginOrNull == null)
                {
                    throw new InvalidOperationException(
                        "The course card position could not be resolved.");
                }

                Point cardCenter = cardOriginOrNull.Value
                    + new Vector(
                        courseCard.Bounds.Width / 2.0,
                        courseCard.Bounds.Height / 2.0);
                window.MouseMove(cardCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();

                Assert.True(courseCard.IsPointerOver);
                Assert.Equal(
                    restingColor,
                    getRequiredSolidColor(courseCard.Background));
                assertTransparent(itemPresenter.Background);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void ResponsivePaneHeadersExposeDismissActions()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_300.0));
        CourseBrowserView courseBrowser = new CourseBrowserView();
        courseBrowser.DataContext = workspace;
        PlanInspectorView inspector = new PlanInspectorView();
        inspector.DataContext = workspace;

        Grid panels = new Grid();
        panels.ColumnDefinitions.Add(new ColumnDefinition(
            new GridLength(1.0, GridUnitType.Star)));
        panels.ColumnDefinitions.Add(new ColumnDefinition(
            new GridLength(1.0, GridUnitType.Star)));
        Grid.SetColumn(inspector, 1);
        panels.Children.Add(courseBrowser);
        panels.Children.Add(inspector);

        Window window = createPanelWindow(panels);
        window.Width = 768.0;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button closeCoursePane = findRequiredControl<Button>(
                courseBrowser,
                "CloseCoursePaneButton");
            Button closeInspectorPane = findRequiredControl<Button>(
                inspector,
                "CloseInspectorPaneButton");

            Assert.False(closeCoursePane.IsVisible);
            Assert.True(closeInspectorPane.IsVisible);
            Assert.Equal(
                "과목 찾기 패널 닫기",
                AutomationProperties.GetName(closeCoursePane));
            Assert.Equal(
                "내 계획 패널 닫기",
                AutomationProperties.GetName(closeInspectorPane));

            workspace.ToggleInspectorPaneCommand.Execute(null);
            Assert.True(workspace.IsInspectorPaneOpen);
            closeInspectorPane.Command?.Execute(null);
            Assert.False(workspace.IsInspectorPaneOpen);

            workspace.applyWorkspaceWidth(new WorkspaceWidth(960.0));
            Dispatcher.UIThread.RunJobs();

            Assert.True(closeCoursePane.IsVisible);
            Assert.True(closeInspectorPane.IsVisible);
            workspace.ToggleCoursePaneCommand.Execute(null);
            Assert.True(workspace.IsCoursePaneOpen);
            closeCoursePane.Command?.Execute(null);
            Assert.False(workspace.IsCoursePaneOpen);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static Window createPanelWindow(Control content)
    {
        Window window = new Window();
        window.Width = INSPECTOR_WIDTH;
        window.Height = MINIMUM_WORKSPACE_HEIGHT;
        window.Content = content;
        return window;
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
                "The required workspace control was not found: " + controlName);
        }

        return controlOrNull;
    }

    private static void assertListDelegatesFocusToCommand(
        ListBox listBox,
        ICommand nestedActionCommand)
    {
        Control? itemContainerOrNull = listBox.ContainerFromIndex(0);
        Assert.NotNull(itemContainerOrNull);
        if (itemContainerOrNull == null)
        {
            throw new InvalidOperationException(
                "The first virtualized workspace card was not realized.");
        }

        Control itemContainer = itemContainerOrNull;
        Button? nestedActionOrNull = itemContainer
            .GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(
                candidate => ReferenceEquals(candidate.Command, nestedActionCommand));
        Assert.NotNull(nestedActionOrNull);
        if (nestedActionOrNull == null)
        {
            throw new InvalidOperationException(
                "The workspace card action was not realized.");
        }

        Assert.False(listBox.Focusable);
        Assert.False(listBox.IsTabStop);
        Assert.False(itemContainer.Focusable);
        Assert.False(itemContainer.IsTabStop);
        Assert.Contains(
            listBox.GetVisualDescendants(),
            descendant => descendant is VirtualizingStackPanel);

        Assert.True(nestedActionOrNull.Focusable);
        Assert.True(nestedActionOrNull.IsTabStop);
        Assert.True(nestedActionOrNull.Focus(NavigationMethod.Tab));
        Dispatcher.UIThread.RunJobs();
        Assert.True(nestedActionOrNull.IsKeyboardFocusWithin);
        Assert.Equal(new Thickness(2.0), nestedActionOrNull.BorderThickness);
    }

    private static Color getRequiredSolidColor(IBrush? brushOrNull)
    {
        ISolidColorBrush? solidBrushOrNull = brushOrNull as ISolidColorBrush;
        Assert.NotNull(solidBrushOrNull);
        if (solidBrushOrNull == null)
        {
            throw new InvalidOperationException(
                "The course card surface was not a solid color.");
        }

        return solidBrushOrNull.Color;
    }

    private static void assertTransparent(IBrush? brushOrNull)
    {
        if (brushOrNull == null)
        {
            return;
        }

        ISolidColorBrush? solidBrushOrNull = brushOrNull as ISolidColorBrush;
        Assert.NotNull(solidBrushOrNull);
        if (solidBrushOrNull == null)
        {
            throw new InvalidOperationException(
                "The list item surface was not a solid color.");
        }

        Assert.Equal(byte.MinValue, solidBrushOrNull.Color.A);
    }
}
