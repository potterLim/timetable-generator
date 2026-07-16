using System;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Presentation.Recommendations;
using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PlannerWorkspaceSmokeTests
{
    [AvaloniaFact]
    public void SearchAndCourseCommandsUpdateTheActivePlan()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        int originalCourseCount = workspace.ActivePlan.UnconfirmedCourses.Count;

        workspace.SearchText = "세미나";

        CourseSearchItem visibleCourse = Assert.Single(workspace.VisibleCourses);
        Assert.False(visibleCourse.IsAdded);

        workspace.AddCourseCommand.Execute(visibleCourse);

        Assert.True(visibleCourse.IsAdded);
        Assert.Equal(originalCourseCount + 1, workspace.ActivePlan.UnconfirmedCourses.Count);

        PlanCourseItem addedCourse = workspace.ActivePlan.UnconfirmedCourses[
            workspace.ActivePlan.UnconfirmedCourses.Count - 1];
        workspace.RemoveCourseCommand.Execute(addedCourse);

        Assert.False(visibleCourse.IsAdded);
        Assert.Equal(originalCourseCount, workspace.ActivePlan.UnconfirmedCourses.Count);
    }

    [AvaloniaFact]
    public void WorkspaceWidthSelectsResponsivePaneModes()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_200.0));

        Assert.Equal(EWorkspaceLayoutMode.Medium, workspace.LayoutMode);
        Assert.True(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsInspectorPaneToggleVisible);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(960.0));

        Assert.Equal(EWorkspaceLayoutMode.Compact, workspace.LayoutMode);
        Assert.False(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsCoursePaneToggleVisible);
    }

    [AvaloniaFact]
    public async Task RecommendationNavigationWrapsAndChangesTheSampleScheduleAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ScheduleRecommendation firstRecommendation = workspace.ActiveRecommendation;

        workspace.NextRecommendationCommand.Execute(null);

        Assert.Equal("2 / 2", workspace.RecommendationSummary);
        Assert.NotSame(firstRecommendation, workspace.ActiveRecommendation);

        workspace.PreviousRecommendationCommand.Execute(null);

        Assert.Equal("1 / 2", workspace.RecommendationSummary);
        Assert.Same(firstRecommendation, workspace.ActiveRecommendation);
    }

    [AvaloniaFact]
    public void EveryTimeNotProvidedOfferingCanBeSelectedExplicitly()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.SearchText = "세미나";
        CourseSearchItem seminar = Assert.Single(workspace.VisibleCourses);
        Assert.Equal(2, seminar.SelectionOptions.Count);
        CourseSelectionOption secondOffering = seminar.SelectionOptions[1];
        Assert.True(secondOffering.IsTimeNotProvided);

        seminar.SelectedSelectionOption = secondOffering;
        workspace.AddCourseCommand.Execute(seminar);

        PlanCourseItem selectedCourse = Assert.Single(
            workspace.ActivePlan.UnconfirmedCourses);
        Assert.Contains("02분반", selectedCourse.MeetingDisplayText);
        Assert.Contains("충돌 자동 검증 제외", selectedCourse.MeetingDisplayText);
        Assert.False(seminar.IsSelectionEnabled);
    }

    [AvaloniaFact]
    public async Task PlanSwitchRestoresThePersistedOfferingSelectionAsync()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.SearchText = "세미나";
            CourseSearchItem seminar = Assert.Single(workspace.VisibleCourses);
            CourseSelectionOption secondOffering = seminar.SelectionOptions[1];
            seminar.SelectedSelectionOption = secondOffering;
            workspace.AddCourseCommand.Execute(seminar);

            workspace.ActivePlan = workspace.Plans[1];
            Assert.False(seminar.IsAdded);
            workspace.ActivePlan = workspace.Plans[0];

            Assert.True(seminar.IsAdded);
            Assert.False(seminar.IsSelectionEnabled);
            Assert.True(
                seminar.SelectedSelectionOption.Represents(secondOffering.Selection));
            await workspace.RecommendationRefreshTask;
        }
    }

    [AvaloniaFact]
    public void PlanSwitchClosesEditsBeforeCommandsCanMutateAnotherPlan()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            int originalPlanCount = workspace.Plans.Count;
            string originalPrimaryName = workspace.Plans[0].DisplayName;

            workspace.BeginDeletePlanCommand.Execute(null);
            Assert.True(workspace.IsDeletePlanConfirmationVisible);
            workspace.ActivePlan = workspace.Plans[1];
            Assert.False(workspace.IsDeletePlanConfirmationVisible);
            workspace.ConfirmDeletePlanCommand.Execute(null);
            Assert.Equal(originalPlanCount, workspace.Plans.Count);

            workspace.BeginRenamePlanCommand.Execute(null);
            workspace.PlanNameDraft = "잘못 적용되면 안 되는 이름";
            workspace.ActivePlan = workspace.Plans[0];
            Assert.False(workspace.IsRenamingPlan);
            workspace.ConfirmRenamePlanCommand.Execute(null);
            Assert.Equal(originalPrimaryName, workspace.ActivePlan.DisplayName);
        }
    }

    [AvaloniaFact]
    public async Task CourseMutationCancelsStaleRecommendationWorkAsync()
    {
        BlockingScheduleRecommendationProvider recommendationProvider =
            new BlockingScheduleRecommendationProvider();
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace(recommendationProvider))
        {
            await recommendationProvider.FirstCallStarted.WaitAsync(
                TimeSpan.FromSeconds(5.0));
            workspace.SearchText = "세미나";
            CourseSearchItem seminar = Assert.Single(workspace.VisibleCourses);

            workspace.AddCourseCommand.Execute(seminar);

            await recommendationProvider.FirstCallCanceled.WaitAsync(
                TimeSpan.FromSeconds(5.0));
            await recommendationProvider.SecondCallStarted.WaitAsync(
                TimeSpan.FromSeconds(5.0));
            Assert.True(workspace.IsRecommendationCalculating);
        }
    }

    [AvaloniaFact]
    public void NamedPlansCanBeCreatedRenamedAndDeleted()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        Assert.Equal(2, workspace.Plans.Count);

        workspace.AddPlanCommand.Execute(null);
        Assert.Equal(3, workspace.Plans.Count);
        Assert.True(workspace.IsRenamingPlan);

        workspace.PlanNameDraft = "집중 수업 계획";
        workspace.ConfirmRenamePlanCommand.Execute(null);
        Assert.Equal("집중 수업 계획", workspace.ActivePlan.DisplayName);

        workspace.BeginDeletePlanCommand.Execute(null);
        Assert.True(workspace.IsDeletePlanConfirmationVisible);
        workspace.ConfirmDeletePlanCommand.Execute(null);

        Assert.Equal(2, workspace.Plans.Count);
        Assert.DoesNotContain(
            workspace.Plans,
            plan => plan.DisplayName == "집중 수업 계획");
    }
}
