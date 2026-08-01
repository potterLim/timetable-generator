using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ProductWorkspaceInteractionTests
{
    [AvaloniaFact]
    public void ScheduleSurfacePreservesInspectorUntilExplicitCloseAction()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(1200.0));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1200.0);

        try
        {
            window.Show();
            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            workspace.BeginRenamePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsInspectorPaneOpen);

            workspace.CancelPlanNameCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            ScheduleWorkspaceView scheduleWorkspace = host.GetVisualDescendants().OfType<ScheduleWorkspaceView>().Single();
            Grid scheduleSurface = findRequiredControl<Grid>(scheduleWorkspace, "ScheduleContentSurface");
            Point surfacePosition = findRequiredPosition(scheduleSurface, window);
            Point clickPosition = new Point(surfacePosition.X + 8.0, surfacePosition.Y + scheduleSurface.Bounds.Height - 8.0);
            window.MouseMove(clickPosition, RawInputModifiers.None);
            window.MouseDown(clickPosition, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(clickPosition, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsInspectorPaneOpen);

            workspace.CloseInspectorPaneCommand.Execute(null);

            Assert.False(workspace.IsInspectorPaneOpen);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void WideWorkspaceKeepsInspectorOverlayFromReflowingSchedule()
    {
        const double WIDE_WIDTH = 1_300.0;

        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(WIDE_WIDTH));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, WIDE_WIDTH);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ScheduleWorkspaceView scheduleWorkspace = host.GetVisualDescendants().OfType<ScheduleWorkspaceView>().Single();
            double widthWithCoursePane = scheduleWorkspace.Bounds.Width;

            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            double widthWithBothPanes = scheduleWorkspace.Bounds.Width;
            Border inspectorPaneHost = findRequiredControl<Border>(host, "InspectorPaneHost");
            Assert.Contains("overlay", inspectorPaneHost.Classes);
            Assert.Equal(widthWithCoursePane, widthWithBothPanes, 3);

            workspace.ToggleCoursePaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            double widthWithInspectorPane = scheduleWorkspace.Bounds.Width;
            Assert.InRange(widthWithInspectorPane - widthWithBothPanes, workspace.CoursePaneWidth - 1.0, workspace.CoursePaneWidth + 1.0);

            workspace.CloseInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            double widthWithoutPanes = scheduleWorkspace.Bounds.Width;
            Assert.Equal(widthWithInspectorPane, widthWithoutPanes, 3);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void EscapeClosesWideInspectorOverlayAndReturnsFocusToItsAction()
    {
        const double WIDE_WIDTH = 1_300.0;

        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(WIDE_WIDTH));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, WIDE_WIDTH);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ScheduleWorkspaceView scheduleWorkspace = host.GetVisualDescendants().OfType<ScheduleWorkspaceView>().Single();
            Button openInspector = findRequiredControl<Button>(scheduleWorkspace, "OpenInspectorPaneButton");
            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Button closeInspector = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == "CloseInspectorPaneButton");
            Assert.True(closeInspector.IsKeyboardFocusWithin);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, string.Empty);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsInspectorPaneOpen);
            Assert.True(openInspector.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void CompactToolbarActionOpensCoursePaneWithoutDismissingInspector()
    {
        const double COMPACT_WIDTH = 900.0;

        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(COMPACT_WIDTH));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, COMPACT_WIDTH);

        try
        {
            window.Show();
            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            ScheduleWorkspaceView scheduleWorkspace = host.GetVisualDescendants().OfType<ScheduleWorkspaceView>().Single();
            Button openCoursePane = findRequiredControl<Button>(scheduleWorkspace, "OpenCoursePaneButton");
            Border inspectorPaneHost = findRequiredControl<Border>(host, "InspectorPaneHost");

            Assert.Contains("overlay", inspectorPaneHost.Classes);

            Point buttonPosition = findRequiredPosition(openCoursePane, window);
            Point clickPosition = new Point(buttonPosition.X + (openCoursePane.Bounds.Width / 2.0), buttonPosition.Y + (openCoursePane.Bounds.Height / 2.0));

            window.MouseMove(clickPosition, RawInputModifiers.None);
            window.MouseDown(clickPosition, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(clickPosition, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsCoursePaneOpen);
            Assert.True(workspace.IsInspectorPaneOpen);

            workspace.ToggleCoursePaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsCoursePaneOpen);
            Assert.True(workspace.IsInspectorPaneOpen);

            Grid scheduleSurface = findRequiredControl<Grid>(scheduleWorkspace, "ScheduleContentSurface");
            Point surfacePosition = findRequiredPosition(scheduleSurface, window);
            Point scheduleClickPosition = new Point(surfacePosition.X + 8.0, surfacePosition.Y + scheduleSurface.Bounds.Height - 8.0);

            window.MouseMove(scheduleClickPosition, RawInputModifiers.None);
            window.MouseDown(scheduleClickPosition, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(scheduleClickPosition, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsInspectorPaneOpen);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task HeaderActionsFitAndOpenInspectorAtMediumBreakpointAsync()
    {
        const double MEDIUM_BREAKPOINT_WIDTH = 1_080.0;

        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        workspace.applyWorkspaceWidth(new WorkspaceWidth(MEDIUM_BREAKPOINT_WIDTH));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, MEDIUM_BREAKPOINT_WIDTH);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ScheduleWorkspaceView scheduleWorkspace = host.GetVisualDescendants().OfType<ScheduleWorkspaceView>().Single();
            Grid commandBar = findRequiredControl<Grid>(scheduleWorkspace, "WorkspaceCommandBar");
            StackPanel supportingActions = findRequiredControl<StackPanel>(scheduleWorkspace, "WorkspaceSupportingActions");
            StackPanel headerActions = findRequiredControl<StackPanel>(scheduleWorkspace, "WorkspaceHeaderActions");
            Button openCoursePane = findRequiredControl<Button>(scheduleWorkspace, "OpenCoursePaneButton");
            Button scheduleViewMode = findRequiredControl<Button>(scheduleWorkspace, "ScheduleViewModeButton");
            Button addPersonalSchedule = findRequiredControl<Button>(scheduleWorkspace, "WorkspaceAddPersonalScheduleButton");
            Button openInspector = findRequiredControl<Button>(scheduleWorkspace, "OpenInspectorPaneButton");
            Button export = findRequiredControl<Button>(scheduleWorkspace, "ExportScheduleButton");

            Assert.True(openInspector.IsEffectivelyVisible);
            Assert.True(export.IsEffectivelyVisible);
            Assert.False(openCoursePane.IsEffectivelyVisible);
            Assert.Equal("시간표 작업 영역", AutomationProperties.GetName(scheduleWorkspace));
            Assert.Null(scheduleWorkspace.FindControl<TextBlock>("ScheduleWorkspaceTitle"));
            Assert.Equal(40.0, commandBar.Bounds.Height);
            Assert.True(supportingActions.Children.IndexOf(openCoursePane) < supportingActions.Children.IndexOf(scheduleViewMode));
            assertCompoundHeaderButtonAlignment(scheduleViewMode);
            assertCompoundHeaderButtonAlignment(addPersonalSchedule);
            assertCompoundHeaderButtonAlignment(openInspector);
            assertCompoundHeaderButtonAlignment(export);
            Assert.Same(workspace.OpenInspectorPaneCommand, openInspector.Command);
            Assert.Equal("시간표 편집 패널 열기", AutomationProperties.GetName(openInspector));
            Assert.Equal("OpenInspectorPane", AutomationProperties.GetAutomationId(openInspector));
            Assert.Equal("시간표 편집", ToolTip.GetTip(openInspector));
            Assert.Contains(
                openInspector.GetVisualDescendants().OfType<TextBlock>(),
                candidate => candidate.Text == "시간표 편집");
            Assert.True(openInspector.Focusable);
            Assert.True(openInspector.IsTabStop);
            Assert.DoesNotContain(
                scheduleWorkspace.GetVisualDescendants().OfType<TextBlock>(),
                candidate => candidate.Text == "추천 시간표");
            Assert.True(headerActions.Children.IndexOf(openInspector) < headerActions.Children.IndexOf(export));

            Point commandBarPosition = findRequiredPosition(commandBar, scheduleWorkspace);
            Point supportingPosition = findRequiredPosition(supportingActions, scheduleWorkspace);
            Point headerPosition = findRequiredPosition(headerActions, scheduleWorkspace);
            Point scheduleViewModePosition = findRequiredPosition(scheduleViewMode, scheduleWorkspace);
            Point addPersonalSchedulePosition = findRequiredPosition(addPersonalSchedule, scheduleWorkspace);
            Point openInspectorPosition = findRequiredPosition(openInspector, scheduleWorkspace);
            Point exportPosition = findRequiredPosition(export, scheduleWorkspace);

            double supportingRight = supportingPosition.X + supportingActions.Bounds.Width;
            double headerRight = headerPosition.X + headerActions.Bounds.Width;
            double scheduleViewModeCenterY = scheduleViewModePosition.Y + (scheduleViewMode.Bounds.Height / 2.0);
            double addPersonalScheduleCenterY = addPersonalSchedulePosition.Y + (addPersonalSchedule.Bounds.Height / 2.0);
            double openInspectorRight = openInspectorPosition.X + openInspector.Bounds.Width;
            double openInspectorCenterY = openInspectorPosition.Y + (openInspector.Bounds.Height / 2.0);
            double exportCenterY = exportPosition.Y + (export.Bounds.Height / 2.0);

            Assert.Equal(18.0, commandBarPosition.Y);
            Assert.True(supportingRight <= headerPosition.X + 1.0);
            Assert.True(headerRight <= scheduleWorkspace.Bounds.Width + 1.0);
            Assert.True(openInspectorRight <= exportPosition.X + 1.0);
            Assert.InRange(Math.Abs(scheduleViewModeCenterY - addPersonalScheduleCenterY), 0.0, 1.0);
            Assert.InRange(Math.Abs(openInspectorCenterY - exportCenterY), 0.0, 1.0);

            workspace.IsCoursePaneOpen = false;
            Dispatcher.UIThread.RunJobs();

            Assert.True(openCoursePane.IsEffectivelyVisible);
            Point openCoursePanePosition = findRequiredPosition(openCoursePane, scheduleWorkspace);
            scheduleViewModePosition = findRequiredPosition(scheduleViewMode, scheduleWorkspace);
            Assert.True(openCoursePanePosition.X + openCoursePane.Bounds.Width <= scheduleViewModePosition.X + 1.0);

            workspace.IsCoursePaneOpen = true;
            Dispatcher.UIThread.RunJobs();

            Assert.False(openCoursePane.IsEffectivelyVisible);

            openInspector.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsInspectorPaneOpen);
            Button closeInspector = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == "CloseInspectorPaneButton");
            Assert.True(closeInspector.IsEffectivelyVisible);
            Assert.True(closeInspector.IsKeyboardFocusWithin);

            closeInspector.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsInspectorPaneOpen);
            Assert.True(openInspector.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }
}
