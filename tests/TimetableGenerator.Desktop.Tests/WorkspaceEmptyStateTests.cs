using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class WorkspaceEmptyStateTests
{
    [AvaloniaFact]
    public async Task WorkspaceShowsOnlyActionsRelevantToTheActivePlanAsync()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.ActivePlan = workspace.Plans[1];
        await workspace.RecommendationRefreshTask;

        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1_200.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ScheduleWorkspaceView scheduleWorkspace =
                findRequiredDescendant<ScheduleWorkspaceView>(host);
            PlanInspectorView planInspector =
                findRequiredDescendant<PlanInspectorView>(host);

            assertEmptyScheduleState(scheduleWorkspace);
            assertEmptyInspectorState(planInspector);

            workspace.ActivePlan = workspace.Plans[0];
            await workspace.RecommendationRefreshTask;
            Dispatcher.UIThread.RunJobs();

            assertPopulatedScheduleState(scheduleWorkspace);
            assertPopulatedInspectorState(planInspector);

            PlanCourseItem scheduledCourse = Assert.Single(
                workspace.ActivePlan.ScheduledCourses);
            workspace.RemoveCourseCommand.Execute(scheduledCourse);
            workspace.SearchText = "세미나";
            CourseSearchItem seminar = Assert.Single(workspace.VisibleCourses);
            workspace.AddCourseCommand.Execute(seminar);
            await workspace.RecommendationRefreshTask;
            Dispatcher.UIThread.RunJobs();

            assertEmptyScheduleState(scheduleWorkspace);
            assertTimeNotProvidedInspectorState(planInspector);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static void assertEmptyScheduleState(
        ScheduleWorkspaceView scheduleWorkspace)
    {
        StackPanel recommendationActions = findRequiredControl<StackPanel>(
            scheduleWorkspace,
            "RecommendationActions");
        Grid scheduleBoardContainer = findRequiredControl<Grid>(
            scheduleWorkspace,
            "ScheduleBoardContainer");
        Border scheduleEmptyState = findRequiredControl<Border>(
            scheduleWorkspace,
            "ScheduleEmptyState");
        StackPanel recommendationFooter = findRequiredControl<StackPanel>(
            scheduleWorkspace,
            "RecommendationFooter");
        Button exportButton = findRequiredControl<Button>(
            scheduleWorkspace,
            "ExportScheduleButton");

        Assert.False(recommendationActions.IsVisible);
        Assert.False(scheduleBoardContainer.IsVisible);
        Assert.True(scheduleEmptyState.IsVisible);
        Assert.False(recommendationFooter.IsVisible);
        Assert.False(exportButton.IsEffectivelyVisible);
        Assert.False(exportButton.IsEnabled);
    }

    private static void assertPopulatedScheduleState(
        ScheduleWorkspaceView scheduleWorkspace)
    {
        StackPanel recommendationActions = findRequiredControl<StackPanel>(
            scheduleWorkspace,
            "RecommendationActions");
        Grid scheduleBoardContainer = findRequiredControl<Grid>(
            scheduleWorkspace,
            "ScheduleBoardContainer");
        Border scheduleEmptyState = findRequiredControl<Border>(
            scheduleWorkspace,
            "ScheduleEmptyState");
        StackPanel recommendationFooter = findRequiredControl<StackPanel>(
            scheduleWorkspace,
            "RecommendationFooter");
        Button exportButton = findRequiredControl<Button>(
            scheduleWorkspace,
            "ExportScheduleButton");

        Assert.True(recommendationActions.IsVisible);
        Assert.True(scheduleBoardContainer.IsVisible);
        Assert.False(scheduleEmptyState.IsVisible);
        Assert.True(recommendationFooter.IsVisible);
        Assert.True(exportButton.IsEffectivelyVisible);
        Assert.True(exportButton.IsEnabled);
    }

    private static void assertEmptyInspectorState(PlanInspectorView planInspector)
    {
        Border emptyPlanState = findRequiredControl<Border>(
            planInspector,
            "EmptyPlanState");
        ListBox scheduledCourses = findRequiredControl<ListBox>(
            planInspector,
            "ScheduledCoursesList");
        Expander timeNotProvidedCourses = findRequiredControl<Expander>(
            planInspector,
            "TimeNotProvidedCoursesExpander");
        Border recommendationPolicy = findRequiredControl<Border>(
            planInspector,
            "RecommendationPolicyCard");

        Assert.True(emptyPlanState.IsVisible);
        Assert.False(scheduledCourses.IsVisible);
        Assert.False(timeNotProvidedCourses.IsVisible);
        Assert.False(recommendationPolicy.IsVisible);
    }

    private static void assertPopulatedInspectorState(
        PlanInspectorView planInspector)
    {
        Border emptyPlanState = findRequiredControl<Border>(
            planInspector,
            "EmptyPlanState");
        ListBox scheduledCourses = findRequiredControl<ListBox>(
            planInspector,
            "ScheduledCoursesList");
        Expander timeNotProvidedCourses = findRequiredControl<Expander>(
            planInspector,
            "TimeNotProvidedCoursesExpander");
        Border recommendationPolicy = findRequiredControl<Border>(
            planInspector,
            "RecommendationPolicyCard");

        Assert.False(emptyPlanState.IsVisible);
        Assert.True(scheduledCourses.IsVisible);
        Assert.False(timeNotProvidedCourses.IsVisible);
        Assert.True(recommendationPolicy.IsVisible);
    }

    private static void assertTimeNotProvidedInspectorState(
        PlanInspectorView planInspector)
    {
        Border emptyPlanState = findRequiredControl<Border>(
            planInspector,
            "EmptyPlanState");
        ListBox scheduledCourses = findRequiredControl<ListBox>(
            planInspector,
            "ScheduledCoursesList");
        Expander timeNotProvidedCourses = findRequiredControl<Expander>(
            planInspector,
            "TimeNotProvidedCoursesExpander");
        Border recommendationPolicy = findRequiredControl<Border>(
            planInspector,
            "RecommendationPolicyCard");

        Assert.False(emptyPlanState.IsVisible);
        Assert.False(scheduledCourses.IsVisible);
        Assert.True(timeNotProvidedCourses.IsVisible);
        Assert.True(recommendationPolicy.IsVisible);
    }

    private static TControl findRequiredDescendant<TControl>(Control root)
        where TControl : Control
    {
        TControl? controlOrNull = root.GetVisualDescendants()
            .OfType<TControl>()
            .FirstOrDefault();
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The required workspace descendant was not found: "
                + typeof(TControl).Name);
        }

        return controlOrNull;
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
}
