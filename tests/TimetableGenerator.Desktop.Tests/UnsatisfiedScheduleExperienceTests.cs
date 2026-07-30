using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class UnsatisfiedScheduleExperienceTests
{
    [AvaloniaFact]
    public async Task UnsatisfiedCoursesWithoutPersonalSchedulesShowRecoveryStateAsync()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        IScheduleRecommendationProvider recommendationProvider = new ForcedConflictRecommendationProvider(document.Catalog);
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(recommendationProvider))
        {
            await workspace.RecommendationRefreshTask;

            Assert.True(workspace.HasUnsatisfiedScheduleConstraints);
            Assert.True(workspace.IsUnsatisfiedScheduleEmpty);
            Assert.False(workspace.HasUnsatisfiedPersonalSchedulePreview);
            Assert.False(workspace.CanExportSchedule);

            ScheduleWorkspaceView scheduleWorkspace = new ScheduleWorkspaceView();
            scheduleWorkspace.DataContext = workspace;
            Window window = createWindow(scheduleWorkspace);

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Border recoveryState = findRequiredControl<Border>(scheduleWorkspace, "UnsatisfiedScheduleEmptyState");
                Border ordinaryEmptyState = findRequiredControl<Border>(scheduleWorkspace, "ScheduleEmptyState");
                Button openPlanButton = findRequiredControl<Button>(scheduleWorkspace, "UnsatisfiedScheduleOpenPlanButton");
                Button exportButton = findRequiredControl<Button>(scheduleWorkspace, "ExportScheduleButton");
                string[] recoveryTexts = recoveryState.GetVisualDescendants().OfType<TextBlock>().Select(getTextOrEmpty).ToArray();

                Assert.True(recoveryState.IsEffectivelyVisible);
                Assert.False(ordinaryEmptyState.IsEffectivelyVisible);
                Assert.Contains("현재 선택으로 만들 수 있는 시간표가 없습니다", recoveryTexts);
                Assert.Contains("겹치는 개인 일정이나 제외한 분반을 확인해 보세요.", recoveryTexts);
                Assert.True(openPlanButton.IsEffectivelyVisible);
                Assert.Equal("충돌한 과목 선택을 시간표 편집에서 확인", AutomationProperties.GetName(openPlanButton));
                Assert.Empty(ordinaryEmptyState.GetVisualDescendants().OfType<Button>());
                Assert.False(exportButton.IsEffectivelyVisible);
                Assert.False(exportButton.IsEnabled);

                Assert.True(workspace.IsInspectorPaneOpen);
                workspace.OpenInspectorPaneCommand.Execute(null);
                workspace.OpenInspectorPaneCommand.Execute(null);
                Assert.True(workspace.IsInspectorPaneOpen);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task UnsatisfiedCoursesWithPersonalSchedulesShowPreviewAndWarningAsync()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            addPersonalSchedule(
                workspace,
                "월요일 고정 일정",
                EDay.Monday,
                new ScheduleTime(8, 30),
                new ScheduleTime(9, 45));
            addPersonalSchedule(
                workspace,
                "화요일 고정 일정",
                EDay.Tuesday,
                new ScheduleTime(11, 30),
                new ScheduleTime(12, 45));
            await workspace.RecommendationRefreshTask;

            Assert.True(workspace.HasUnsatisfiedScheduleConstraints);
            Assert.False(workspace.IsUnsatisfiedScheduleEmpty);
            Assert.True(workspace.HasUnsatisfiedPersonalSchedulePreview);
            Assert.False(workspace.CanExportSchedule);
            Assert.All(
                workspace.DisplayedSchedule.Entries,
                entry => Assert.IsType<PersonalScheduleEntry>(entry));

            ScheduleWorkspaceView scheduleWorkspace = new ScheduleWorkspaceView();
            scheduleWorkspace.DataContext = workspace;
            Window window = createWindow(scheduleWorkspace);

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Border warningBanner = findRequiredControl<Border>(scheduleWorkspace, "UnsatisfiedPersonalScheduleBanner");
                Grid scheduleBoardContainer = findRequiredControl<Grid>(scheduleWorkspace, "ScheduleBoardContainer");
                Border centralRecoveryState = findRequiredControl<Border>(scheduleWorkspace, "UnsatisfiedScheduleEmptyState");
                Button exportButton = findRequiredControl<Button>(scheduleWorkspace, "ExportScheduleButton");
                Button openPlanButton = warningBanner.GetVisualDescendants().OfType<Button>().Single();
                string[] warningTexts = warningBanner.GetVisualDescendants().OfType<TextBlock>().Select(getTextOrEmpty).ToArray();

                Assert.True(warningBanner.IsEffectivelyVisible);
                Assert.True(scheduleBoardContainer.IsEffectivelyVisible);
                Assert.False(centralRecoveryState.IsEffectivelyVisible);
                Assert.Contains("과목은 배치하지 못했습니다", warningTexts);
                Assert.Contains("아래에는 개인 일정만 표시됩니다. 겹치는 개인 일정이나 분반 선택을 조정해 보세요.", warningTexts);
                Assert.True(openPlanButton.IsEffectivelyVisible);
                Assert.Equal("선택 과목 확인", openPlanButton.Content);
                Assert.Equal("충돌한 과목 선택을 시간표 편집에서 확인", AutomationProperties.GetName(openPlanButton));
                Assert.False(exportButton.IsEffectivelyVisible);
                Assert.False(exportButton.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        }
    }

    private static Window createWindow(Control content)
    {
        Window window = new Window();
        window.Width = 1_200.0;
        window.Height = 760.0;
        window.Content = content;
        return window;
    }

    private static void addPersonalSchedule(
        PlannerWorkspaceViewModel workspace,
        string title,
        EDay day,
        ScheduleTime start,
        ScheduleTime end)
    {
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        workspace.PersonalScheduleTitleDraft = title;
        selectPersonalScheduleDay(workspace, day);
        workspace.PersonalScheduleStartTimeOrNull = start;
        workspace.PersonalScheduleEndTimeOrNull = end;
        workspace.SavePersonalScheduleCommand.Execute(null);
    }

    private static void selectPersonalScheduleDay(PlannerWorkspaceViewModel workspace, EDay day)
    {
        PersonalScheduleDayOption? optionOrNull =
            workspace.PersonalScheduleDayOptions.FirstOrDefault(
                option => option.Day == day);
        if (optionOrNull == null)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "The personal schedule day option was not found.");
        }

        optionOrNull.IsSelected = true;
    }

    private static TControl findRequiredControl<TControl>(Control root, string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("The required workspace control was not found: " + controlName);
        }

        return controlOrNull;
    }

    private static string getTextOrEmpty(TextBlock textBlock)
    {
        return textBlock.Text == null ? string.Empty : textBlock.Text;
    }

    private sealed class ForcedConflictRecommendationProvider :
        IScheduleRecommendationProvider
    {
        private readonly CourseCatalog mCatalog;

        private readonly ScheduleRecommendationGenerator mGenerator;

        public ForcedConflictRecommendationProvider(CourseCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            mCatalog = catalog;
            mGenerator = new ScheduleRecommendationGenerator();
        }

        public ScheduleRecommendationResult Generate(PlanningPlan plan, ScheduleRecommendationLimit recommendationLimit, CancellationToken cancellationToken)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            PersonalSchedule[] blockingSchedules = new PersonalSchedule[]
            {
                createBlockingSchedule(
                    "월요일 차단",
                    EDay.Monday,
                    new ScheduleTime(8, 30),
                    new ScheduleTime(9, 45)),
                createBlockingSchedule(
                    "화요일 차단",
                    EDay.Tuesday,
                    new ScheduleTime(11, 30),
                    new ScheduleTime(12, 45)),
            };
            PlanningPlan conflictingPlan = new PlanningPlan(plan.Id, plan.Name, plan.CatalogBinding, new PlanningPlanContent(plan.CourseChoiceGroups, plan.UnscheduledOfferingSelections, blockingSchedules));
            ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(mCatalog, conflictingPlan, recommendationLimit);
            return mGenerator.GenerateRecommendations(request, cancellationToken);
        }

        private static PersonalSchedule createBlockingSchedule(string title, EDay day, ScheduleTime start, ScheduleTime end)
        {
            WeeklyTimeRange timeRange = new WeeklyTimeRange(day, new DailyTimeRange(start, end));
            return new PersonalSchedule(PersonalScheduleId.CreateNew(), new PersonalScheduleTitle(title), new WeeklyTimeRange[] { timeRange }, PersonalScheduleDetails.CreateEmpty());
        }
    }
}
