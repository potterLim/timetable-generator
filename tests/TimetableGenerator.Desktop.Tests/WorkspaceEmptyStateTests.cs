using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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

public sealed class WorkspaceEmptyStateTests
{
    [AvaloniaFact]
    public async Task WorkspaceShowsOnlyActionsRelevantToTheActivePlanAsync()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_200.0));
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

            assertEmptyScheduleState(
                scheduleWorkspace,
                false,
                "과목을 선택해 시간표를 구성해 보세요",
                "과목을 선택하면 가능한 시간표를 자동으로 만듭니다.");
            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            assertEmptyInspectorState(planInspector);

            workspace.ActivePlan = workspace.Plans[0];
            await workspace.RecommendationRefreshTask;
            Dispatcher.UIThread.RunJobs();

            workspace.CloseInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            assertPopulatedScheduleState(scheduleWorkspace);
            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            assertPopulatedInspectorState(planInspector);

            PlanCourseChoiceGroupItem scheduledCourseGroup = Assert.Single(
                workspace.ActivePlan.CourseChoiceGroups);
            workspace.RemoveCourseChoiceGroupCommand.Execute(scheduledCourseGroup);
            workspace.SearchText = "세미나";
            CourseSearchItem seminar = Assert.Single(workspace.VisibleCourses);
            workspace.AddCourseCommand.Execute(seminar);
            Assert.True(workspace.CanSaveCourseChoice);
            workspace.SaveCourseChoiceCommand.Execute(null);
            await workspace.RecommendationRefreshTask;
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.HasRecommendations);
            Assert.False(workspace.HasScheduleEntries);

            workspace.CloseInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            assertEmptyScheduleState(
                scheduleWorkspace,
                true,
                "시간이 정해진 과목이 없습니다",
                "시간 미정 과목은 현재 시간표에 유지됩니다.");
            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            assertTimeNotProvidedChoiceInspectorState(planInspector);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static void assertEmptyScheduleState(
        ScheduleWorkspaceView scheduleWorkspace,
        bool areRecommendationActionsVisible,
        string expectedHeading,
        string expectedDescription)
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
        Button exportButton = findRequiredControl<Button>(
            scheduleWorkspace,
            "ExportScheduleButton");
        Button openInspectorPane = findRequiredControl<Button>(
            scheduleWorkspace,
            "OpenInspectorPaneButton");
        TextBlock heading = findRequiredControl<TextBlock>(
            scheduleWorkspace,
            "ScheduleEmptyStateHeading");
        TextBlock description = findRequiredControl<TextBlock>(
            scheduleWorkspace,
            "ScheduleEmptyStateDescription");

        Assert.Equal(
            areRecommendationActionsVisible,
            recommendationActions.IsVisible);
        Assert.False(scheduleBoardContainer.IsVisible);
        Assert.True(scheduleEmptyState.IsVisible);
        Assert.False(exportButton.IsEffectivelyVisible);
        Assert.False(exportButton.IsEnabled);
        Assert.True(openInspectorPane.IsEffectivelyVisible);
        Assert.Empty(scheduleEmptyState.GetVisualDescendants().OfType<Button>());
        Assert.Equal(expectedHeading, heading.Text);
        Assert.Equal(expectedDescription, description.Text);
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
        Button exportButton = findRequiredControl<Button>(
            scheduleWorkspace,
            "ExportScheduleButton");
        Button openInspectorPane = findRequiredControl<Button>(
            scheduleWorkspace,
            "OpenInspectorPaneButton");

        Assert.True(recommendationActions.IsVisible);
        Assert.True(scheduleBoardContainer.IsVisible);
        Assert.False(scheduleEmptyState.IsVisible);
        Assert.True(exportButton.IsEffectivelyVisible);
        Assert.True(exportButton.IsEnabled);
        Assert.True(openInspectorPane.IsEffectivelyVisible);
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
        Border iconSurface = findRequiredControl<Border>(
            planInspector,
            "EmptyPlanStateIconSurface");
        StackPanel emptyPlanStateContent = findRequiredControl<StackPanel>(
            planInspector,
            "EmptyPlanStateContent");
        TextBlock heading = findRequiredControl<TextBlock>(
            planInspector,
            "EmptyPlanStateHeading");
        TextBlock description = findRequiredControl<TextBlock>(
            planInspector,
            "EmptyPlanStateDescription");
        Button addPersonalScheduleButton = findRequiredControl<Button>(
            planInspector,
            "AddPersonalScheduleButton");

        Assert.True(emptyPlanState.IsVisible);
        Assert.False(scheduledCourses.IsVisible);
        Assert.False(timeNotProvidedCourses.IsVisible);
        Assert.Equal(emptyPlanState.Padding.Left, emptyPlanState.Padding.Right);
        Assert.Empty(
            emptyPlanState
                .GetVisualDescendants()
                .OfType<Button>());
        Assert.Equal(HorizontalAlignment.Center, iconSurface.HorizontalAlignment);
        Assert.Equal(TextAlignment.Center, heading.TextAlignment);
        Assert.Equal(TextAlignment.Center, description.TextAlignment);
        Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
        Assert.Equal("선택한 과목이 없습니다", heading.Text);
        Assert.Equal(
            "과목을 선택해 시간표를 구성해 보세요.",
            description.Text);
        Assert.True(addPersonalScheduleButton.IsEffectivelyVisible);
        assertControlSharesHorizontalCenter(
            planInspector,
            emptyPlanStateContent,
            iconSurface);
        assertControlSharesHorizontalCenter(
            planInspector,
            emptyPlanStateContent,
            heading);
        assertControlSharesHorizontalCenter(
            planInspector,
            emptyPlanStateContent,
            description);
        assertExpectedCreditsSummary(planInspector, "0학점");
        assertSectionCounts(planInspector, "수강 선택 (0)", "개인 일정 (0)");
        Assert.Null(planInspector.FindControl<Border>("RecommendationPolicyCard"));
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
        Border personalScheduleEmptyState = findRequiredControl<Border>(
            planInspector,
            "PersonalScheduleEmptyState");

        Assert.False(emptyPlanState.IsVisible);
        Assert.True(scheduledCourses.IsVisible);
        Assert.False(timeNotProvidedCourses.IsVisible);
        Assert.True(personalScheduleEmptyState.IsVisible);
        assertExpectedCreditsSummary(planInspector, "3학점");
        assertSectionCounts(planInspector, "수강 선택 (1)", "개인 일정 (0)");
        TextBlock emptyStateMessage = Assert.IsType<TextBlock>(
            personalScheduleEmptyState.Child);
        Assert.Equal(
            "수업 외 고정 일정을 추가하세요.",
            emptyStateMessage.Text);
        Assert.Equal(
            "수업 외 고정 일정을 추가하세요.",
            ToolTip.GetTip(emptyStateMessage));
    }

    private static void assertTimeNotProvidedChoiceInspectorState(
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

        Assert.False(emptyPlanState.IsVisible);
        Assert.True(scheduledCourses.IsVisible);
        Assert.False(timeNotProvidedCourses.IsVisible);
        assertExpectedCreditsSummary(planInspector, "1학점");
        assertSectionCounts(planInspector, "수강 선택 (1)", "개인 일정 (0)");
    }

    private static void assertExpectedCreditsSummary(
        PlanInspectorView planInspector,
        string expectedCreditsDisplayText)
    {
        TextBlock label = findRequiredControl<TextBlock>(
            planInspector,
            "ExpectedCreditsLabel");
        TextBlock value = findRequiredControl<TextBlock>(
            planInspector,
            "ExpectedCreditsValue");

        Assert.Equal("예상 학점", label.Text);
        Assert.Equal(expectedCreditsDisplayText, value.Text);
        Assert.Equal(
            "예상 학점 " + expectedCreditsDisplayText,
            AutomationProperties.GetName(value));
    }

    private static void assertSectionCounts(
        PlanInspectorView planInspector,
        string expectedScheduledCourseHeading,
        string expectedPersonalScheduleHeading)
    {
        TextBlock scheduledCoursesHeading = findRequiredControl<TextBlock>(
            planInspector,
            "ScheduledCoursesHeading");
        TextBlock personalSchedulesHeading = findRequiredControl<TextBlock>(
            planInspector,
            "PersonalSchedulesHeading");

        Assert.Equal(
            expectedScheduledCourseHeading,
            scheduledCoursesHeading.Text);
        Assert.Equal(
            expectedPersonalScheduleHeading,
            personalSchedulesHeading.Text);
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

    private static void assertControlSharesHorizontalCenter(
        Control coordinateRoot,
        Control container,
        Control centeredControl)
    {
        Point? containerOriginOrNull = container.TranslatePoint(
            new Point(0.0, 0.0),
            coordinateRoot);
        Point? controlOriginOrNull = centeredControl.TranslatePoint(
            new Point(0.0, 0.0),
            coordinateRoot);
        Assert.NotNull(containerOriginOrNull);
        Assert.NotNull(controlOriginOrNull);
        if (containerOriginOrNull == null || controlOriginOrNull == null)
        {
            throw new InvalidOperationException(
                "The empty plan state control position could not be resolved.");
        }

        double containerCenterX = containerOriginOrNull.Value.X
            + (container.Bounds.Width / 2.0);
        double controlCenterX = controlOriginOrNull.Value.X
            + (centeredControl.Bounds.Width / 2.0);
        Assert.InRange(
            Math.Abs(controlCenterX - containerCenterX),
            0.0,
            0.5);
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
