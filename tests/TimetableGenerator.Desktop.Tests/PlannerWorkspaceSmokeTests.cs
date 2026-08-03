using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;
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
        int originalCourseCount = workspace.ActivePlan.CourseChoiceGroups.Count;

        workspace.SearchText = "세미나";

        CourseSearchItem visibleCourse = Assert.Single(workspace.VisibleCourses);
        Assert.False(visibleCourse.IsAdded);

        workspace.AddCourseCommand.Execute(visibleCourse);
        Assert.True(workspace.IsCourseChoiceEditorVisible);
        workspace.SaveCourseChoiceCommand.Execute(null);

        Assert.True(visibleCourse.IsAdded);
        Assert.Equal(originalCourseCount + 1, workspace.ActivePlan.CourseChoiceGroups.Count);

        PlanCourseChoiceGroupItem addedCourse = workspace.ActivePlan.CourseChoiceGroups[workspace.ActivePlan.CourseChoiceGroups.Count - 1];
        workspace.RemoveCourseChoiceGroupCommand.Execute(addedCourse);

        Assert.False(visibleCourse.IsAdded);
        Assert.Equal(originalCourseCount, workspace.ActivePlan.CourseChoiceGroups.Count);
    }

    [AvaloniaFact]
    public void WorkspaceWidthChangesPresentationWithoutDiscardingPaneChoices()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_600.0));

        Assert.Equal(EWorkspaceLayoutMode.ExtraWide, workspace.LayoutMode);
        Assert.Equal(SplitViewDisplayMode.Inline, workspace.CoursePaneDisplayMode);
        Assert.Equal(SplitViewDisplayMode.Inline, workspace.InspectorPaneDisplayMode);
        Assert.Equal(312.0, workspace.CoursePaneWidth);
        Assert.Equal(288.0, workspace.InspectorPaneWidth);
        Assert.True(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsCoursePaneToggleVisible);
        Assert.True(workspace.IsCoursePaneDismissActionVisible);
        Assert.False(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsInspectorPaneToggleVisible);
        Assert.False(workspace.IsInspectorPaneDismissActionVisible);

        workspace.ToggleCoursePaneCommand.Execute(null);
        workspace.OpenInspectorPaneCommand.Execute(null);

        Assert.False(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_300.0));

        Assert.Equal(EWorkspaceLayoutMode.Wide, workspace.LayoutMode);
        Assert.Equal(SplitViewDisplayMode.Inline, workspace.CoursePaneDisplayMode);
        Assert.Equal(SplitViewDisplayMode.Overlay, workspace.InspectorPaneDisplayMode);
        Assert.Equal(312.0, workspace.CoursePaneWidth);
        Assert.Equal(304.0, workspace.InspectorPaneWidth);
        Assert.False(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_200.0));

        Assert.Equal(EWorkspaceLayoutMode.Medium, workspace.LayoutMode);
        Assert.Equal(SplitViewDisplayMode.Inline, workspace.CoursePaneDisplayMode);
        Assert.Equal(SplitViewDisplayMode.Overlay, workspace.InspectorPaneDisplayMode);
        Assert.Equal(320.0, workspace.CoursePaneWidth);
        Assert.Equal(304.0, workspace.InspectorPaneWidth);
        Assert.False(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(960.0));

        Assert.Equal(EWorkspaceLayoutMode.Compact, workspace.LayoutMode);
        Assert.Equal(SplitViewDisplayMode.Overlay, workspace.CoursePaneDisplayMode);
        Assert.Equal(SplitViewDisplayMode.Overlay, workspace.InspectorPaneDisplayMode);
        Assert.Equal(320.0, workspace.CoursePaneWidth);
        Assert.Equal(304.0, workspace.InspectorPaneWidth);
        Assert.False(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsCoursePaneToggleVisible);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_600.0));

        Assert.Equal(EWorkspaceLayoutMode.ExtraWide, workspace.LayoutMode);
        Assert.False(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);

        workspace.ToggleCoursePaneCommand.Execute(null);
        workspace.CloseInspectorPaneCommand.Execute(null);

        Assert.True(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);
    }

    [AvaloniaFact]
    public void CompactWorkspacePrioritizesInspectorAndPreservesItBehindCoursePane()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();

        workspace.applyWorkspaceWidth(new WorkspaceWidth(960.0));

        Assert.Equal(EWorkspaceLayoutMode.Compact, workspace.LayoutMode);
        Assert.False(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);
        Assert.True(workspace.IsCoursePaneToggleVisible);
        Assert.True(workspace.IsInspectorPaneToggleVisible);

        workspace.ToggleCoursePaneCommand.Execute(null);

        Assert.True(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);

        workspace.OpenInspectorPaneCommand.Execute(null);

        Assert.False(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);

        workspace.ToggleCoursePaneCommand.Execute(null);

        Assert.True(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);
    }

    [AvaloniaFact]
    public void EscapeClosesTheTopmostResponsiveOverlay()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_300.0));

        workspace.OpenInspectorPaneCommand.Execute(null);
        workspace.tryCloseTopmostTransientWorkspaceOverlay();

        Assert.True(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(960.0));

        workspace.ToggleCoursePaneCommand.Execute(null);
        workspace.tryCloseTopmostTransientWorkspaceOverlay();

        Assert.False(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);

        workspace.OpenInspectorPaneCommand.Execute(null);
        workspace.tryCloseTopmostTransientWorkspaceOverlay();

        Assert.False(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);

        workspace.OpenInspectorPaneCommand.Execute(null);
        workspace.ToggleCoursePaneCommand.Execute(null);
        workspace.tryCloseTopmostTransientWorkspaceOverlay();

        Assert.False(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);

        workspace.tryCloseTopmostTransientWorkspaceOverlay();

        Assert.False(workspace.IsCoursePaneOpen);
        Assert.False(workspace.IsInspectorPaneOpen);

        workspace.applyWorkspaceWidth(new WorkspaceWidth(1_600.0));
        workspace.ToggleCoursePaneCommand.Execute(null);
        workspace.OpenInspectorPaneCommand.Execute(null);
        workspace.tryCloseTopmostTransientWorkspaceOverlay();

        Assert.True(workspace.IsCoursePaneOpen);
        Assert.True(workspace.IsInspectorPaneOpen);
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
        Assert.Equal(2, workspace.PngExportCandidates.Count);
        Assert.True(workspace.CanExportAllPngCandidates);
        Assert.Same(firstRecommendation, workspace.PngExportCandidates[0].Schedule);
        ScheduleBoardPresentation firstBoard = Assert.IsType<ScheduleBoardPresentation>(workspace.DisplayedScheduleBoard);
        Assert.Equal(new ScheduleBoardTimeBoundary(510), firstBoard.Layout.TimeAxis.Start);
        Assert.Equal(new ScheduleBoardTimeBoundary(690), firstBoard.Layout.TimeAxis.End);
        int sharedDayCount = firstBoard.Layout.DayRange.DayCount;

        workspace.NextRecommendationCommand.Execute(null);

        Assert.Equal("2 / 2", workspace.RecommendationSummary);
        Assert.NotSame(firstRecommendation, workspace.ActiveRecommendation);
        Assert.All(
            workspace.ActiveRecommendation.Entries,
            entry => Assert.True(
                entry.TimeRange.Start.MinutesFromMidnight >= 600));
        ScheduleBoardPresentation secondBoard = Assert.IsType<ScheduleBoardPresentation>(workspace.DisplayedScheduleBoard);
        Assert.Equal(new ScheduleBoardTimeBoundary(690), secondBoard.Layout.TimeAxis.Start);
        Assert.Equal(new ScheduleBoardTimeBoundary(810), secondBoard.Layout.TimeAxis.End);
        Assert.Equal(sharedDayCount, secondBoard.Layout.DayRange.DayCount);

        workspace.PreviousRecommendationCommand.Execute(null);

        Assert.Equal("1 / 2", workspace.RecommendationSummary);
        Assert.Same(firstRecommendation, workspace.ActiveRecommendation);
    }

    [AvaloniaFact]
    public async Task RecommendationNavigationPersistsTheExactCombinationAsync()
    {
        ImmediatePlanningWorkspaceStore planningWorkspaceStore = new ImmediatePlanningWorkspaceStore();
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(planningWorkspaceStore))
        {
            await workspace.RecommendationRefreshTask;
            Task completedRefreshTask = workspace.RecommendationRefreshTask;

            workspace.NextRecommendationCommand.Execute(null);
            await workspace.FlushAutosaveAsync(CancellationToken.None);

            Assert.Equal("2 / 2", workspace.RecommendationSummary);
            Assert.Same(completedRefreshTask, workspace.RecommendationRefreshTask);
            Assert.False(workspace.IsRecommendationCalculating);
            PlanningWorkspace savedWorkspace = Assert.IsType<PlanningWorkspace>(planningWorkspaceStore.LastSavedWorkspaceOrNull);
            ScheduleRecommendationBookmark savedBookmark = Assert.IsType<ScheduleRecommendationBookmark>(savedWorkspace.GetActivePlan().LastViewedRecommendationOrNull);
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
        ScheduleRecommendationBookmark bookmark = new ScheduleRecommendationBookmark(
            new OfferingId[]
            {
                new OfferingId("offering-programming-alternative"),
            });
        ImmediatePlanningWorkspaceStore planningWorkspaceStore = new ImmediatePlanningWorkspaceStore();
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(bookmark, planningWorkspaceStore))
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
    public void EveryTimeNotProvidedOfferingCanBePreferredOrExcluded()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.SearchText = "세미나";
        CourseSearchItem seminar = Assert.Single(workspace.VisibleCourses);
        Assert.Equal(2, seminar.SelectionOptions.Count);
        CourseSelectionOption secondOffering = seminar.SelectionOptions[1];
        Assert.True(secondOffering.IsTimeNotProvided);

        workspace.AddCourseCommand.Execute(seminar);
        CourseChoiceDraftCourseItem draft = Assert.Single(workspace.CourseChoiceDraftCourses);
        draft.Offerings[0].SelectExcludedCommand.Execute(null);
        draft.Offerings[1].SelectPreferredCommand.Execute(null);
        workspace.SaveCourseChoiceCommand.Execute(null);

        CourseChoiceGroup seminarGroup = workspace.ActivePlan.Plan
            .CourseChoiceGroups
            .Single(group => group.CourseCandidates.Any(
                candidate => candidate.CourseId == seminar.CourseId));
        CourseCandidate candidate = Assert.Single(seminarGroup.CourseCandidates);
        Assert.Equal(EOfferingPreference.Excluded, candidate.OfferingCandidates[0].Preference);
        Assert.Equal(EOfferingPreference.Preferred, candidate.OfferingCandidates[1].Preference);
        Assert.Empty(workspace.ActivePlan.TimeNotProvidedCourses);
        Assert.False(seminar.IsSelectionEnabled);
    }

    [AvaloniaFact]
    public void MultiOfferingCourseUsesOneConciseMetadataLine()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.SearchText = "프로그래밍";
            CourseSearchItem programming = Assert.Single(workspace.VisibleCourses);

            Assert.False(programming.HasSingleOfferingDetails);
            Assert.Empty(programming.SingleOfferingDetailsDisplayText);
            Assert.Equal("2개 분반 · 3학점", programming.InstructorCreditDisplayText);
        }
    }

    [AvaloniaFact]
    public async Task PlanSwitchRestoresThePersistedOfferingSelectionAsync()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.SearchText = "세미나";
            CourseSearchItem seminar = Assert.Single(workspace.VisibleCourses);
            workspace.AddCourseCommand.Execute(seminar);
            CourseChoiceDraftCourseItem draft = Assert.Single(workspace.CourseChoiceDraftCourses);
            draft.Offerings[0].SelectExcludedCommand.Execute(null);
            draft.Offerings[1].SelectPreferredCommand.Execute(null);
            workspace.SaveCourseChoiceCommand.Execute(null);

            workspace.ActivePlan = workspace.Plans[1];
            Assert.False(seminar.IsAdded);
            workspace.ActivePlan = workspace.Plans[0];

            Assert.True(seminar.IsAdded);
            Assert.False(seminar.IsSelectionEnabled);
            workspace.EditOrRemoveSelectedCourseCommand.Execute(seminar);
            Assert.True(workspace.IsCourseChoiceEditorVisible);
            Assert.True(workspace.CourseChoiceDraftCourses[0].Offerings[0].IsExcluded);
            Assert.True(workspace.CourseChoiceDraftCourses[0].Offerings[1].IsPreferred);
            await workspace.RecommendationRefreshTask;
        }
    }

    [AvaloniaFact]
    public void PlanSwitchClosesEditsBeforeCommandsCanMutateAnotherPlan()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
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
            workspace.ConfirmPlanNameCommand.Execute(null);
            Assert.Equal(originalPrimaryName, workspace.ActivePlan.DisplayName);
        }
    }

    [AvaloniaFact]
    public async Task CourseMutationCancelsStaleRecommendationWorkAsync()
    {
        BlockingScheduleRecommendationProvider recommendationProvider = new BlockingScheduleRecommendationProvider();
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(recommendationProvider))
        {
            await recommendationProvider.FirstCallStarted.WaitAsync(TimeSpan.FromSeconds(5.0));
            workspace.SearchText = "세미나";
            CourseSearchItem seminar = Assert.Single(workspace.VisibleCourses);

            workspace.AddCourseCommand.Execute(seminar);
            workspace.SaveCourseChoiceCommand.Execute(null);

            await recommendationProvider.FirstCallCanceled.WaitAsync(TimeSpan.FromSeconds(5.0));
            await recommendationProvider.SecondCallStarted.WaitAsync(TimeSpan.FromSeconds(5.0));
            Assert.True(workspace.IsRecommendationCalculating);
        }
    }

    [AvaloniaFact]
    public async Task ShutdownCancelsRecommendationWorkBeforeCompletingAutosaveAsync()
    {
        BlockingScheduleRecommendationProvider recommendationProvider = new BlockingScheduleRecommendationProvider();
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(recommendationProvider))
        using (CancellationTokenSource timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5.0)))
        {
            await recommendationProvider.FirstCallStarted.WaitAsync(timeoutSource.Token);

            await workspace.CompleteAutosaveAsync(timeoutSource.Token);

            await recommendationProvider.FirstCallCanceled.WaitAsync(timeoutSource.Token);
        }
    }

    [AvaloniaFact]
    public async Task ClearingActivePlanRequiresConfirmationAndAutosavesEmptyContentAsync()
    {
        ImmediatePlanningWorkspaceStore planningWorkspaceStore = new ImmediatePlanningWorkspaceStore();
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(planningWorkspaceStore))
        {
            PlanId originalPlanId = workspace.ActivePlan.PlanId;
            PlanName originalPlanName = workspace.ActivePlan.Name;
            PlanCatalogBinding originalBinding = workspace.ActivePlan.Plan.CatalogBinding;
            int originalPlanCount = workspace.Plans.Count;

            Assert.True(workspace.CanClearActivePlan);
            Assert.True(workspace.BeginClearActivePlanCommand.CanExecute(null));
            workspace.BeginClearActivePlanCommand.Execute(null);

            Assert.True(workspace.IsClearActivePlanConfirmationVisible);
            Assert.True(workspace.IsPlanEditingOverlayVisible);
            Assert.False(workspace.IsWorkspaceInteractionEnabled);
            Assert.Equal("시간표 비우기 확인", workspace.PlanEditingDialogAccessibleName);
            Assert.Equal(originalPlanName.Value, workspace.PlanPendingClearName);
            Assert.Equal("'" + originalPlanName.Value + "'의 모든 내용을 지웁니다.", workspace.PlanClearDescription);
            Assert.False(workspace.ActivePlan.IsCompletelyEmpty);

            workspace.CancelClearActivePlanCommand.Execute(null);

            Assert.False(workspace.IsClearActivePlanConfirmationVisible);
            Assert.True(workspace.IsWorkspaceInteractionEnabled);
            Assert.False(workspace.ActivePlan.IsCompletelyEmpty);

            workspace.BeginClearActivePlanCommand.Execute(null);
            workspace.ConfirmClearActivePlanCommand.Execute(null);
            await workspace.RecommendationRefreshTask;
            await workspace.FlushAutosaveAsync(CancellationToken.None);

            Assert.False(workspace.IsPlanEditingOverlayVisible);
            Assert.True(workspace.IsWorkspaceInteractionEnabled);
            Assert.Equal(originalPlanCount, workspace.Plans.Count);
            Assert.Equal(originalPlanId, workspace.ActivePlan.PlanId);
            Assert.Same(originalPlanName, workspace.ActivePlan.Name);
            Assert.Same(originalBinding, workspace.ActivePlan.Plan.CatalogBinding);
            Assert.True(workspace.ActivePlan.IsCompletelyEmpty);
            Assert.Empty(workspace.ActivePlan.Plan.CourseChoiceGroups);
            Assert.Empty(workspace.ActivePlan.Plan.UnscheduledOfferingSelections);
            Assert.Empty(workspace.ActivePlan.Plan.PersonalSchedules);
            Assert.Null(workspace.ActivePlan.Plan.LastViewedRecommendationOrNull);
            Assert.False(workspace.CanClearActivePlan);
            Assert.False(workspace.BeginClearActivePlanCommand.CanExecute(null));
            PlanningWorkspace savedWorkspace = Assert.IsType<PlanningWorkspace>(planningWorkspaceStore.LastSavedWorkspaceOrNull);
            Assert.Empty(savedWorkspace.GetActivePlan().CourseChoiceGroups);
            Assert.Empty(savedWorkspace.GetActivePlan().UnscheduledOfferingSelections);
            Assert.Empty(savedWorkspace.GetActivePlan().PersonalSchedules);
        }
    }

    [AvaloniaFact]
    public void NamedPlansCanBeCreatedRenamedAndDeleted()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        Assert.Equal(2, workspace.Plans.Count);

        workspace.AddPlanCommand.Execute(null);
        Assert.Equal(2, workspace.Plans.Count);
        Assert.True(workspace.IsCreatingPlan);
        Assert.False(workspace.IsRenamingPlan);
        Assert.Equal("시간표 이름", workspace.PlanNameEditorTitle);
        Assert.Equal("만들기", workspace.PlanNameEditorPrimaryActionText);

        workspace.PlanNameDraft = "집중 수업 계획";
        workspace.ConfirmPlanNameCommand.Execute(null);
        Assert.Equal(3, workspace.Plans.Count);
        Assert.False(workspace.IsPlanNameEditorVisible);
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
    public void PlanTabRenameCommandTargetsTheClickedPlan()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            PlanId activePlanId = workspace.ActivePlan.PlanId;
            PlanTabItem planToRename = workspace.Plans[1];
            PlanId renamedPlanId = planToRename.PlanId;

            planToRename.RenameCommand.Execute(null);

            Assert.True(workspace.IsRenamingPlan);
            Assert.Equal(planToRename.DisplayName, workspace.PlanNameDraft);
            Assert.Equal(activePlanId, workspace.ActivePlan.PlanId);

            workspace.PlanNameDraft = "오후 수업 시간표";
            workspace.ConfirmPlanNameCommand.Execute(null);

            Assert.False(workspace.IsRenamingPlan);
            Assert.Equal(activePlanId, workspace.ActivePlan.PlanId);
            Assert.Contains(
                workspace.Plans,
                plan => plan.PlanId == renamedPlanId
                    && plan.DisplayName == "오후 수업 시간표");
        }
    }

    [AvaloniaFact]
    public void DuplicatePlanNameShowsSpecificValidationMessage()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            string existingPlanName = workspace.Plans[0].DisplayName;
            PlanTabItem planToRename = workspace.Plans[1];
            planToRename.RenameCommand.Execute(null);
            workspace.PlanNameDraft = existingPlanName.ToUpperInvariant();

            workspace.ConfirmPlanNameCommand.Execute(null);

            Assert.True(workspace.IsRenamingPlan);
            Assert.Equal("같은 이름의 시간표가 이미 있습니다.", workspace.PlanNameValidationMessage);
        }
    }

    [AvaloniaFact]
    public void NewPlanUsesAnAvailableDefaultName()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.ActivePlan = workspace.Plans[0];
            workspace.BeginRenamePlanCommand.Execute(null);
            workspace.PlanNameDraft = "2026-2학기 시간표";
            workspace.ConfirmPlanNameCommand.Execute(null);
            workspace.ActivePlan = workspace.Plans[1];
            workspace.BeginRenamePlanCommand.Execute(null);
            workspace.PlanNameDraft = "2026-2학기 시간표(2)";
            workspace.ConfirmPlanNameCommand.Execute(null);

            workspace.AddPlanCommand.Execute(null);

            Assert.Equal("2026-2학기 시간표 (3)", workspace.PlanNameDraft);
            Assert.Equal(2, workspace.Plans.Count);
            Assert.True(workspace.IsCreatingPlan);

            workspace.ConfirmPlanNameCommand.Execute(null);

            Assert.Equal("2026-2학기 시간표 (3)", workspace.ActivePlan.DisplayName);
            Assert.Equal(3, workspace.Plans.Count);
            Assert.False(workspace.IsPlanNameEditorVisible);
        }
    }

    [AvaloniaFact]
    public void CancelingPlanCreationLeavesTheWorkspaceUnchanged()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            int originalPlanCount = workspace.Plans.Count;
            PlanId originalActivePlanId = workspace.ActivePlan.PlanId;

            workspace.AddPlanCommand.Execute(null);

            Assert.True(workspace.IsCreatingPlan);
            Assert.Equal(originalPlanCount, workspace.Plans.Count);
            Assert.Equal(originalActivePlanId, workspace.ActivePlan.PlanId);

            workspace.CancelPlanNameCommand.Execute(null);

            Assert.False(workspace.IsPlanNameEditorVisible);
            Assert.Equal(originalPlanCount, workspace.Plans.Count);
            Assert.Equal(originalActivePlanId, workspace.ActivePlan.PlanId);
        }
    }

    [AvaloniaFact]
    public void PlanCreationValidatesTheDraftBeforeMutatingTheWorkspace()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            int originalPlanCount = workspace.Plans.Count;
            string existingPlanName = workspace.Plans[0].DisplayName;

            workspace.AddPlanCommand.Execute(null);
            workspace.PlanNameDraft = existingPlanName.ToUpperInvariant();
            workspace.ConfirmPlanNameCommand.Execute(null);

            Assert.True(workspace.IsCreatingPlan);
            Assert.Equal(originalPlanCount, workspace.Plans.Count);
            Assert.Equal("같은 이름의 시간표가 이미 있습니다.", workspace.PlanNameValidationMessage);

            workspace.PlanNameDraft = "새 학기 시간표";
            workspace.ConfirmPlanNameCommand.Execute(null);

            Assert.False(workspace.IsPlanNameEditorVisible);
            Assert.Equal(originalPlanCount + 1, workspace.Plans.Count);
            Assert.Equal("새 학기 시간표", workspace.ActivePlan.DisplayName);
        }
    }

    [AvaloniaFact]
    public void DeletingPlansTargetsTheVisibleTabAndAllowsAnEmptyWorkspace()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            PlanTabItem activePlan = workspace.Plans[0];
            PlanTabItem planToClose = workspace.Plans[1];
            workspace.ActivePlan = activePlan;

            Assert.True(planToClose.CanClose);
            Assert.True(planToClose.CloseCommand.CanExecute(null));
            Assert.Contains(planToClose.DisplayName, planToClose.CloseButtonAccessibleName);
            Assert.Equal("시간표 삭제", planToClose.CloseButtonHelpText);

            planToClose.CloseCommand.Execute(null);

            Assert.True(workspace.IsDeletePlanConfirmationVisible);
            Assert.True(workspace.IsPlanEditingOverlayVisible);
            Assert.False(workspace.IsWorkspaceInteractionEnabled);
            Assert.Equal(planToClose.DisplayName, workspace.PlanPendingDeletionName);
            Assert.Same(activePlan, workspace.ActivePlan);

            workspace.ConfirmDeletePlanCommand.Execute(null);

            PlanTabItem remainingPlan = Assert.Single(workspace.Plans);
            Assert.Equal(activePlan.PlanId, remainingPlan.PlanId);
            Assert.True(remainingPlan.CanClose);
            Assert.True(remainingPlan.CloseCommand.CanExecute(null));
            Assert.Equal("시간표 삭제", remainingPlan.CloseButtonHelpText);
            Assert.False(workspace.IsPlanEditingOverlayVisible);
            Assert.True(workspace.IsWorkspaceInteractionEnabled);

            remainingPlan.CloseCommand.Execute(null);
            workspace.ConfirmDeletePlanCommand.Execute(null);

            Assert.Empty(workspace.Plans);
            Assert.Null(workspace.ActivePlanOrNull);
            Assert.False(workspace.HasActivePlan);
            Assert.True(workspace.IsWorkspaceEmpty);
            Assert.False(workspace.CanDeleteActivePlan);
            Assert.False(workspace.BeginDeletePlanCommand.CanExecute(null));
        }
    }

    [AvaloniaFact]
    public void EscapeCancelsPlanEditingBeforeClosingResponsivePanes()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            workspace.BeginRenamePlanCommand.Execute(null);
            Assert.True(workspace.IsPlanEditingOverlayVisible);

            workspace.tryCloseTopmostTransientWorkspaceOverlay();

            Assert.False(workspace.IsRenamingPlan);
            Assert.False(workspace.IsPlanEditingOverlayVisible);
            Assert.True(workspace.IsWorkspaceInteractionEnabled);
        }
    }
}
