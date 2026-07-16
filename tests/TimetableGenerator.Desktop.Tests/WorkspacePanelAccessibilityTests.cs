using System;
using System.Linq;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class WorkspacePanelAccessibilityTests
{
    private const double INSPECTOR_WIDTH = 384.0;
    private const double MINIMUM_PRODUCT_WINDOW_HEIGHT = 640.0;
    private const double PRODUCT_NAVIGATION_HEIGHT = 112.0;
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
                workspace.RemoveCourseCommand);
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
}
