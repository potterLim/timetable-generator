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
using Avalonia.Styling;
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
        workspace.SaveCourseChoiceCommand.Execute(null);

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
            StackPanel scrollableContent = findRequiredControl<StackPanel>(
                inspector,
                "PlanInspectorScrollableContent");
            Border personalScheduleEmptyState = findRequiredControl<Border>(
                inspector,
                "PersonalScheduleEmptyState");

            if (scrollViewer.Extent.Height > scrollViewer.Viewport.Height)
            {
                scrollViewer.ScrollToEnd();
                Dispatcher.UIThread.RunJobs();
            }

            Point? emptyStateTopLeftOrNull = personalScheduleEmptyState.TranslatePoint(
                new Point(0.0, 0.0),
                scrollViewer);
            Assert.NotNull(emptyStateTopLeftOrNull);
            if (emptyStateTopLeftOrNull == null)
            {
                throw new InvalidOperationException(
                    "The personal schedule state was not attached to the inspector viewport.");
            }

            Point emptyStateTopLeft = emptyStateTopLeftOrNull.Value;
            double emptyStateBottom =
                emptyStateTopLeft.Y + personalScheduleEmptyState.Bounds.Height;
            Assert.True(emptyStateTopLeft.Y >= 0.0);
            Assert.True(emptyStateBottom <= scrollViewer.Viewport.Height + 1.0);

            Point? scrollableContentPositionOrNull =
                scrollableContent.TranslatePoint(
                    new Point(0.0, 0.0),
                    scrollViewer);
            Assert.NotNull(scrollableContentPositionOrNull);
            if (scrollableContentPositionOrNull == null)
            {
                throw new InvalidOperationException(
                    "The inspector content was not attached to its viewport.");
            }

            double contentRight = scrollableContentPositionOrNull.Value.X
                + scrollableContent.Bounds.Width;
            double contentGutter = scrollViewer.Bounds.Width - contentRight;
            Assert.InRange(contentGutter, 15.0, 17.0);
            Assert.Null(inspector.FindControl<Button>("RenameActivePlanButton"));
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void InspectorComplexContentActionsExposeExplicitAccessibleNames()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        PlanInspectorView inspector = new PlanInspectorView();
        inspector.DataContext = workspace;
        Window window = createPanelWindow(inspector);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Border emptyPlanState = findRequiredControl<Border>(
                inspector,
                "EmptyPlanState");
            Assert.Empty(
                emptyPlanState
                    .GetVisualDescendants()
                    .OfType<Button>());
            Button addPersonalScheduleButton = findRequiredControl<Button>(
                inspector,
                "AddPersonalScheduleButton");
            Assert.True(
                ReferenceEquals(
                    addPersonalScheduleButton.Command,
                    workspace.BeginAddPersonalScheduleCommand));
            Assert.Equal(
                "개인 일정 추가",
                AutomationProperties.GetName(addPersonalScheduleButton));

            Button managementButton = findRequiredControl<Button>(
                inspector,
                "PlanManagementButton");
            TextBlock managementTitle = findRequiredControl<TextBlock>(
                inspector,
                "PlanManagementTitle");
            Assert.Equal(
                workspace.ActivePlan.DisplayName,
                managementTitle.Text);
            Assert.Equal(
                workspace.ActivePlan.DisplayName,
                AutomationProperties.GetName(managementButton));
            Assert.Equal(
                "시간표 관리",
                AutomationProperties.GetHelpText(managementButton));
            Assert.Equal("시간표 관리", ToolTip.GetTip(managementButton));
            Assert.Equal(
                2,
                (int)AutomationProperties.GetHeadingLevel(managementButton));
            Flyout managementFlyout = Assert.IsType<Flyout>(
                managementButton.Flyout);
            managementFlyout.ShowAt(managementButton);
            Dispatcher.UIThread.RunJobs();

            Control managementContent = Assert.IsAssignableFrom<Control>(
                managementFlyout.Content);
            assertActionAccessibleName(
                managementContent,
                workspace.BeginRenamePlanCommand,
                "현재 시간표 이름 바꾸기");
            assertActionAccessibleName(
                managementContent,
                workspace.BeginClearActivePlanCommand,
                "현재 시간표 비우기");
            assertActionAccessibleName(
                managementContent,
                workspace.BeginDeletePlanCommand,
                "현재 시간표 삭제");
            managementFlyout.Hide();
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

            assertCourseListDelegatesFocusToAction(
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

                ComboBox departmentFilter = findRequiredControl<ComboBox>(
                    courseBrowser,
                    "DepartmentFilter");
                ComboBox requirementFilter = findRequiredControl<ComboBox>(
                    courseBrowser,
                    "RequirementFilter");
                Assert.Equal(
                    searchBox.Bounds.Width,
                    departmentFilter.Bounds.Width,
                    3);
                Assert.Equal(
                    departmentFilter.Bounds.Width,
                    requirementFilter.Bounds.Width,
                    3);
                Assert.Equal(
                    departmentFilter.Bounds.X,
                    requirementFilter.Bounds.X,
                    3);
                Assert.True(
                    requirementFilter.Bounds.Top
                        >= departmentFilter.Bounds.Bottom + 7.5);

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
    public void MultiTimeNotProvidedCourseUsesTheAccessibleSharedEditorAction()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.ActivePlan = workspace.Plans[1];
            workspace.SearchText = "세미나";
            CourseSearchItem seminar = Assert.Single(workspace.VisibleCourses);
            CourseBrowserView courseBrowser = new CourseBrowserView();
            courseBrowser.DataContext = workspace;
            Window window = createPanelWindow(courseBrowser);

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                ComboBox[] inlineSelectors = courseBrowser.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .ToArray();
                Assert.Equal(2, inlineSelectors.Length);

                Button selectionButton = courseBrowser.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(
                        candidate => candidate.IsVisible
                            && ReferenceEquals(
                                candidate.Command,
                                workspace.AddCourseCommand)
                            && ReferenceEquals(
                                candidate.CommandParameter,
                                seminar));
                Assert.Equal(
                    seminar.Name + " 수강 선택 설정 열기",
                    AutomationProperties.GetName(selectionButton));
                Assert.Null(selectionButton.Flyout);

                selectionButton.Command?.Execute(selectionButton.CommandParameter);
                Dispatcher.UIThread.RunJobs();

                Assert.True(workspace.IsCourseChoiceEditorVisible);
                CourseChoiceDraftCourseItem draft = Assert.Single(
                    workspace.CourseChoiceDraftCourses);
                Assert.Equal(2, draft.Offerings.Count);
                Assert.All(
                    draft.Offerings,
                    offering => Assert.False(string.IsNullOrWhiteSpace(
                        offering.PreferenceAccessibleName)));
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
                workspace.SaveCourseChoiceCommand.Execute(null);
                courseResults.SelectedIndex = 0;
                Dispatcher.UIThread.RunJobs();

                Assert.Contains("added", courseCard.Classes);
                Assert.Equal(
                    getRequiredApplicationColor(
                        "SelectionSurfaceBrush",
                        courseCard.ActualThemeVariant),
                    getRequiredSolidColor(courseCard.Background));
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
                    getRequiredApplicationColor(
                        "SelectionHoverSurfaceBrush",
                        courseCard.ActualThemeVariant),
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
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createPanelWindow(host);
        window.Width = 1_300.0;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button closeCoursePane = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == "CloseCoursePaneButton");
            Button openInspectorPane = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == "OpenInspectorPaneButton");

            Assert.True(closeCoursePane.IsEffectivelyVisible);
            Assert.True(openInspectorPane.IsEffectivelyVisible);
            Assert.Equal(
                "과목 찾기 패널 닫기",
                AutomationProperties.GetName(closeCoursePane));

            openInspectorPane.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Button closeInspectorPane = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == "CloseInspectorPaneButton");
            Assert.True(workspace.IsInspectorPaneOpen);
            Assert.True(closeInspectorPane.IsEffectivelyVisible);
            Assert.True(closeInspectorPane.IsKeyboardFocusWithin);
            Assert.Equal(
                "시간표 관리 패널 닫기",
                AutomationProperties.GetName(closeInspectorPane));
            Assert.Equal("시간표 관리 닫기", ToolTip.GetTip(closeInspectorPane));

            closeInspectorPane.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsInspectorPaneOpen);
            Assert.True(openInspectorPane.IsKeyboardFocusWithin);

            closeCoursePane.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsCoursePaneOpen);
            Button openCoursePane = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => ReferenceEquals(
                        candidate.Command,
                        workspace.ToggleCoursePaneCommand)
                        && candidate.IsEffectivelyVisible
                        && candidate.Name != "CloseCoursePaneButton");
            Assert.True(openCoursePane.IsKeyboardFocusWithin);

            openCoursePane.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsCoursePaneOpen);
            Assert.True(closeCoursePane.IsEffectivelyVisible);

            workspace.applyWorkspaceWidth(new WorkspaceWidth(960.0));
            Dispatcher.UIThread.RunJobs();

            Assert.True(closeCoursePane.IsEffectivelyVisible);
            Assert.False(closeInspectorPane.IsEffectivelyVisible);

            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsCoursePaneOpen);
            Assert.True(workspace.IsInspectorPaneOpen);
            Assert.False(closeCoursePane.IsEffectivelyVisible);
            Assert.True(closeInspectorPane.IsEffectivelyVisible);

            workspace.applyWorkspaceWidth(new WorkspaceWidth(1_600.0));
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsInspectorPaneOpen);
            Assert.True(closeInspectorPane.IsEffectivelyVisible);
            workspace.ToggleCoursePaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsCoursePaneOpen);
            Assert.True(closeCoursePane.IsEffectivelyVisible);
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
        assertListDelegatesFocusToAction(
            listBox,
            candidate => ReferenceEquals(candidate.Command, nestedActionCommand));
    }

    private static void assertCourseListDelegatesFocusToAction(
        ListBox listBox,
        ICommand directAddCommand)
    {
        assertListDelegatesFocusToAction(
            listBox,
            candidate => candidate.IsEffectivelyVisible
                && (ReferenceEquals(candidate.Command, directAddCommand)
                    || candidate.Flyout != null));
    }

    private static void assertListDelegatesFocusToAction(
        ListBox listBox,
        Func<Button, bool> isExpectedAction)
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
            .FirstOrDefault(isExpectedAction);
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

    private static void assertActionAccessibleName(
        Control root,
        ICommand command,
        string expectedName)
    {
        Button action = root.GetVisualDescendants()
            .OfType<Button>()
            .Single(candidate => ReferenceEquals(candidate.Command, command));
        Assert.Equal(expectedName, AutomationProperties.GetName(action));
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
