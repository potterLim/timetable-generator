using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Presentation.Recommendations;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PlannerWorkspaceSmokeTests
{
    [AvaloniaFact]
    public void SearchAndCourseCommandsUpdateTheActivePlan()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        int originalCourseCount =
            workspace.ActivePlan.TimeNotProvidedCourses.Count;

        workspace.SearchText = "세미나";

        CourseSearchItem visibleCourse = Assert.Single(workspace.VisibleCourses);
        Assert.False(visibleCourse.IsAdded);

        workspace.AddCourseCommand.Execute(visibleCourse);

        Assert.True(visibleCourse.IsAdded);
        Assert.Equal(
            originalCourseCount + 1,
            workspace.ActivePlan.TimeNotProvidedCourses.Count);

        TimeNotProvidedCourseItem addedCourse =
            workspace.ActivePlan.TimeNotProvidedCourses[
                workspace.ActivePlan.TimeNotProvidedCourses.Count - 1];
        workspace.RemoveTimeNotProvidedCourseCommand.Execute(addedCourse);

        Assert.False(visibleCourse.IsAdded);
        Assert.Equal(
            originalCourseCount,
            workspace.ActivePlan.TimeNotProvidedCourses.Count);
    }

    [AvaloniaFact]
    public void WorkspaceWidthSelectsResponsivePaneModes()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_600.0));

        Assert.Equal(EWorkspaceLayoutMode.ExtraWide, workspace.LayoutMode);
        Assert.Equal(312.0, workspace.CoursePaneWidth);
        Assert.Equal(288.0, workspace.InspectorPaneWidth);
        Assert.True(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);
        Assert.False(workspace.IsInspectorPaneToggleVisible);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_300.0));

        Assert.Equal(EWorkspaceLayoutMode.Wide, workspace.LayoutMode);
        Assert.Equal(312.0, workspace.CoursePaneWidth);
        Assert.Equal(304.0, workspace.InspectorPaneWidth);
        Assert.True(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsInspectorPaneToggleVisible);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_200.0));

        Assert.Equal(EWorkspaceLayoutMode.Medium, workspace.LayoutMode);
        Assert.Equal(320.0, workspace.CoursePaneWidth);
        Assert.Equal(304.0, workspace.InspectorPaneWidth);
        Assert.True(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsInspectorPaneToggleVisible);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(960.0));

        Assert.Equal(EWorkspaceLayoutMode.Compact, workspace.LayoutMode);
        Assert.Equal(320.0, workspace.CoursePaneWidth);
        Assert.Equal(304.0, workspace.InspectorPaneWidth);
        Assert.False(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsCoursePaneToggleVisible);
    }

    [AvaloniaFact]
    public void WorkspacePaneWidthRejectsInvalidValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new WorkspacePaneWidth(0.0);
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new WorkspacePaneWidth(double.NaN);
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new WorkspacePaneWidth(double.PositiveInfinity);
            });
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
    public async Task RecommendationNavigationPersistsTheExactCombinationAsync()
    {
        ImmediatePlanningWorkspaceStore planningWorkspaceStore =
            new ImmediatePlanningWorkspaceStore();
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace(planningWorkspaceStore))
        {
            await workspace.RecommendationRefreshTask;
            Task completedRefreshTask = workspace.RecommendationRefreshTask;

            workspace.NextRecommendationCommand.Execute(null);
            await workspace.FlushAutosaveAsync(CancellationToken.None);

            Assert.Equal("2 / 2", workspace.RecommendationSummary);
            Assert.Same(completedRefreshTask, workspace.RecommendationRefreshTask);
            Assert.False(workspace.IsRecommendationCalculating);
            PlanningWorkspace savedWorkspace = Assert.IsType<PlanningWorkspace>(
                planningWorkspaceStore.LastSavedWorkspaceOrNull);
            ScheduleRecommendationBookmark savedBookmark =
                Assert.IsType<ScheduleRecommendationBookmark>(
                    savedWorkspace.GetActivePlan().LastViewedRecommendationOrNull);
            Assert.True(savedBookmark.HasSameScheduledOfferingIds(
                new OfferingId[]
                {
                    new OfferingId("offering-programming-alternative"),
                }));
        }
    }

    [AvaloniaFact]
    public async Task LoadedWorkspaceRestoresEachPlansLastRecommendationAsync()
    {
        ScheduleRecommendationBookmark bookmark =
            new ScheduleRecommendationBookmark(
                new OfferingId[]
                {
                    new OfferingId("offering-programming-alternative"),
                });
        ImmediatePlanningWorkspaceStore planningWorkspaceStore =
            new ImmediatePlanningWorkspaceStore();
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace(
                bookmark,
                planningWorkspaceStore))
        {
            await workspace.RecommendationRefreshTask;

            Assert.Equal("2 / 2", workspace.RecommendationSummary);

            workspace.ActivePlan = workspace.Plans[1];
            await workspace.RecommendationRefreshTask;
            Assert.Equal("0 / 0", workspace.RecommendationSummary);

            workspace.ActivePlan = workspace.Plans[0];
            await workspace.RecommendationRefreshTask;
            Assert.Equal("2 / 2", workspace.RecommendationSummary);
        }
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

        TimeNotProvidedCourseItem selectedCourse = Assert.Single(
            workspace.ActivePlan.TimeNotProvidedCourses);
        Assert.Contains("02분반", selectedCourse.MeetingDisplayText);
        Assert.Contains("충돌 자동 검증 제외", selectedCourse.MeetingDisplayText);
        Assert.False(seminar.IsSelectionEnabled);
    }

    [AvaloniaFact]
    public void MultiOfferingCourseUsesOneConciseMetadataLine()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.SearchText = "프로그래밍";
            CourseSearchItem programming = Assert.Single(
                workspace.VisibleCourses);

            Assert.False(programming.HasSingleOfferingDetails);
            Assert.Empty(programming.SingleOfferingDetailsDisplayText);
            Assert.Equal("2개 분반 · 3학점", programming.InstructorCreditDisplayText);
        }
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
    public async Task ShutdownCancelsRecommendationWorkBeforeCompletingAutosaveAsync()
    {
        BlockingScheduleRecommendationProvider recommendationProvider =
            new BlockingScheduleRecommendationProvider();
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace(recommendationProvider))
        using (CancellationTokenSource timeoutSource =
            new CancellationTokenSource(TimeSpan.FromSeconds(5.0)))
        {
            await recommendationProvider.FirstCallStarted.WaitAsync(
                timeoutSource.Token);

            await workspace.CompleteAutosaveAsync(timeoutSource.Token);

            await recommendationProvider.FirstCallCanceled.WaitAsync(
                timeoutSource.Token);
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

    [AvaloniaFact]
    public void ClosingAPlanTargetsTheVisibleTabAndProtectsTheLastPlan()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            PlanTabItem activePlan = workspace.Plans[0];
            PlanTabItem planToClose = workspace.Plans[1];
            workspace.ActivePlan = activePlan;

            Assert.True(planToClose.CanClose);
            Assert.True(planToClose.CloseCommand.CanExecute(null));
            Assert.Contains(planToClose.DisplayName, planToClose.CloseButtonAccessibleName);
            Assert.Equal("계획 닫기", planToClose.CloseButtonHelpText);

            planToClose.CloseCommand.Execute(null);

            Assert.True(workspace.IsDeletePlanConfirmationVisible);
            Assert.True(workspace.IsPlanEditingOverlayVisible);
            Assert.False(workspace.IsWorkspaceInteractionEnabled);
            Assert.Equal(planToClose.DisplayName, workspace.PlanPendingDeletionName);
            Assert.Same(activePlan, workspace.ActivePlan);

            workspace.ConfirmDeletePlanCommand.Execute(null);

            PlanTabItem remainingPlan = Assert.Single(workspace.Plans);
            Assert.Equal(activePlan.PlanId, remainingPlan.PlanId);
            Assert.False(remainingPlan.CanClose);
            Assert.False(remainingPlan.CloseCommand.CanExecute(null));
            Assert.Equal(
                "마지막 계획은 닫을 수 없습니다",
                remainingPlan.CloseButtonHelpText);
            Assert.False(workspace.IsPlanEditingOverlayVisible);
            Assert.True(workspace.IsWorkspaceInteractionEnabled);
        }
    }

    [AvaloniaFact]
    public void EscapeCancelsPlanEditingBeforeClosingResponsivePanes()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.BeginRenamePlanCommand.Execute(null);
            Assert.True(workspace.IsPlanEditingOverlayVisible);

            workspace.closeOverlayPanes();

            Assert.False(workspace.IsRenamingPlan);
            Assert.False(workspace.IsPlanEditingOverlayVisible);
            Assert.True(workspace.IsWorkspaceInteractionEnabled);
        }
    }
}
