using System;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PersonalScheduleInteractionTests
{
    [AvaloniaFact]
    public void AddEditAndDeleteStayInsideTheActivePlan()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();

        try
        {
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            workspace.PersonalScheduleTitleDraft = "랩 미팅";
            workspace.IsTuesdaySelected = true;
            workspace.IsThursdaySelected = true;
            workspace.PersonalScheduleStartTimeOrNull = new TimeSpan(18, 0, 0);
            workspace.PersonalScheduleEndTimeOrNull = new TimeSpan(19, 30, 0);
            workspace.PersonalScheduleSectionDraft = "A";
            workspace.PersonalScheduleInstructorDraft = "김교수";
            workspace.PersonalScheduleLocationDraft = "느헤미야홀";

            workspace.SavePersonalScheduleCommand.Execute(null);

            Assert.False(workspace.IsPersonalScheduleEditorVisible);
            PersonalScheduleItem addedItem = Assert.Single(
                workspace.ActivePlan.PersonalSchedules);
            Assert.Equal("랩 미팅", addedItem.Title);
            Assert.Equal("화·목 · 18:00–19:30", addedItem.TimeSummary);
            Assert.Contains("분반 A", addedItem.DetailsSummary);
            Assert.Contains("김교수", addedItem.DetailsSummary);
            Assert.Contains("느헤미야홀", addedItem.DetailsSummary);

            workspace.ActivePlan = workspace.Plans[1];
            Assert.Empty(workspace.ActivePlan.PersonalSchedules);

            workspace.ActivePlan = workspace.Plans[0];
            PersonalScheduleItem itemToEdit = Assert.Single(
                workspace.ActivePlan.PersonalSchedules);
            workspace.BeginEditPersonalScheduleCommand.Execute(itemToEdit);
            workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
            workspace.PersonalScheduleLocationDraft = string.Empty;
            workspace.SavePersonalScheduleCommand.Execute(null);

            PersonalScheduleItem editedItem = Assert.Single(
                workspace.ActivePlan.PersonalSchedules);
            Assert.Equal("연구실 정기 미팅", editedItem.Title);
            Assert.DoesNotContain("느헤미야홀", editedItem.DetailsSummary);

            workspace.BeginDeletePersonalScheduleCommand.Execute(editedItem);
            Assert.True(workspace.IsDeletePersonalScheduleConfirmationVisible);
            workspace.ConfirmDeletePersonalScheduleCommand.Execute(null);

            Assert.Empty(workspace.ActivePlan.PersonalSchedules);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleEditorUsesACenteredAccessibleModal()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1200.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button addButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => candidate.Name
                        == "WorkspaceAddPersonalScheduleButton");
            addButton.Focus();
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Border workspaceSurface = findRequiredControl<Border>(
                host,
                "PersonalScheduleEditorOverlay");
            Border dialog = findRequiredControl<Border>(
                host,
                "PersonalScheduleEditorDialog");
            TextBox nameInput = host.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(candidate => candidate.Name == "PersonalScheduleNameInput");
            Grid rootWorkspaceSurface = findRequiredControl<Grid>(
                host,
                "WorkspaceSurface");

            Assert.True(workspaceSurface.IsVisible);
            Assert.False(rootWorkspaceSurface.IsEnabled);
            Assert.True(nameInput.IsKeyboardFocusWithin);
            Assert.Equal(
                "PersonalScheduleNameInput",
                AutomationProperties.GetAutomationId(nameInput));
            Assert.Equal(560.0, dialog.Bounds.Width);
            Assert.Equal(
                KeyboardNavigationMode.Cycle,
                KeyboardNavigation.GetTabNavigation(dialog));
            PersonalScheduleEditorView editor = host.GetVisualDescendants()
                .OfType<PersonalScheduleEditorView>()
                .Single();
            Assert.Equal(
                KeyboardNavigationMode.Cycle,
                KeyboardNavigation.GetTabNavigation(editor));
            Assert.Equal(
                "개인 일정 대화상자",
                AutomationProperties.GetName(dialog));

            workspace.SavePersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Border validationSummary = host.GetVisualDescendants()
                .OfType<Border>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleValidationSummary");
            Assert.True(validationSummary.IsVisible);
            Assert.Equal(
                EPersonalScheduleDraftValidationError.TitleRequired,
                workspace.PersonalScheduleValidationError);
            Assert.True(nameInput.IsKeyboardFocusWithin);

            workspace.CancelPersonalScheduleEditCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspaceSurface.IsVisible);
            Assert.True(rootWorkspaceSurface.IsEnabled);
            Assert.True(addButton.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void ClearedRequiredTimeIsRejectedAndFocused()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1200.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            workspace.PersonalScheduleTitleDraft = "점심 약속";
            workspace.IsWednesdaySelected = true;
            TimePicker startTimeInput = host.GetVisualDescendants()
                .OfType<TimePicker>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleStartTimeInput");

            startTimeInput.SelectedTime = null;
            Dispatcher.UIThread.RunJobs();
            workspace.SavePersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Null(workspace.PersonalScheduleStartTimeOrNull);
            Assert.Equal(
                EPersonalScheduleDraftValidationError.StartTimeRequired,
                workspace.PersonalScheduleValidationError);
            Assert.True(startTimeInput.IsKeyboardFocusWithin);
            Assert.Empty(workspace.ActivePlan.PersonalSchedules);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void InvalidEndTimePrecisionIsRejectedAndFocused()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1200.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            workspace.PersonalScheduleTitleDraft = "점심 약속";
            workspace.IsWednesdaySelected = true;
            workspace.PersonalScheduleEndTimeOrNull = new TimeSpan(13, 1, 0);
            TimePicker endTimeInput = host.GetVisualDescendants()
                .OfType<TimePicker>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleEndTimeInput");

            workspace.SavePersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(
                EPersonalScheduleDraftValidationError.EndTimePrecisionInvalid,
                workspace.PersonalScheduleValidationError);
            Assert.True(endTimeInput.IsKeyboardFocusWithin);
            Assert.Empty(workspace.ActivePlan.PersonalSchedules);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void SaveAndDeleteRestoreFocusToALiveWorkspaceControl()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        workspace.PersonalScheduleTitleDraft = "랩 미팅";
        workspace.IsTuesdaySelected = true;
        workspace.SavePersonalScheduleCommand.Execute(null);
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1200.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            PersonalScheduleItem itemToEdit = Assert.Single(
                workspace.ActivePlan.PersonalSchedules);
            Button editButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => AutomationProperties.GetName(candidate)
                        == itemToEdit.EditButtonAccessibleName);
            Assert.True(editButton.Focus());

            workspace.BeginEditPersonalScheduleCommand.Execute(itemToEdit);
            Dispatcher.UIThread.RunJobs();
            workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
            workspace.SavePersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Button addButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => candidate.Name
                        == "WorkspaceAddPersonalScheduleButton");
            Assert.True(addButton.IsKeyboardFocusWithin);

            PersonalScheduleItem itemToDelete = Assert.Single(
                workspace.ActivePlan.PersonalSchedules);
            Button deleteButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => AutomationProperties.GetName(candidate)
                        == itemToDelete.RemoveButtonAccessibleName);
            Assert.True(deleteButton.Focus());

            workspace.BeginDeletePersonalScheduleCommand.Execute(itemToDelete);
            Dispatcher.UIThread.RunJobs();
            workspace.ConfirmDeletePersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(workspace.ActivePlan.PersonalSchedules);
            Assert.True(addButton.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void OpenPersonalScheduleEditorRestoresFocusAfterHostReattachment()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1200.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            TextBox nameInput = host.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(candidate => candidate.Name == "PersonalScheduleNameInput");
            Assert.True(nameInput.IsKeyboardFocusWithin);

            window.Content = null;
            Dispatcher.UIThread.RunJobs();
            window.Content = host;
            Dispatcher.UIThread.RunJobs();

            Assert.True(nameInput.IsKeyboardFocusWithin);
            workspace.SavePersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(
                EPersonalScheduleDraftValidationError.TitleRequired,
                workspace.PersonalScheduleValidationError);
            Assert.True(nameInput.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleBoardCardUsesExactFiveMinutePlacement()
    {
        PersonalSchedule schedule = createPersonalSchedule();
        WeeklyTimeRange timeRange = schedule.TimeRanges[0];
        PersonalScheduleEntry entry = new PersonalScheduleEntry(schedule, timeRange);
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(new ScheduleEntry[] { entry }));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 520.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Grid boardGrid = findRequiredControl<Grid>(scheduleBoard, "BoardGrid");
            Button scheduleCard = boardGrid.Children
                .OfType<Button>()
                .Single();

            Assert.Equal(47, Grid.GetRow(scheduleCard));
            Assert.Equal(12, Grid.GetRowSpan(scheduleCard));
            Assert.Contains("personal", scheduleCard.Classes);
            Assert.Contains(
                "수요일 12:20–13:20",
                AutomationProperties.GetName(scheduleCard));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void EarlyShortScheduleShowsAClockLabelAndUsableTarget()
    {
        DailyTimeRange timeRange = new DailyTimeRange(
            new ScheduleTime(7, 40),
            new ScheduleTime(7, 55));
        PersonalSchedule schedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("아침 약속"),
            new WeeklyTimeRange[]
            {
                new WeeklyTimeRange(EDay.Monday, timeRange),
            },
            PersonalScheduleDetails.CreateEmpty());
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(
                new ScheduleEntry[]
                {
                    new PersonalScheduleEntry(schedule, schedule.TimeRanges[0]),
                }));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 520.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Grid boardGrid = findRequiredControl<Grid>(scheduleBoard, "BoardGrid");
            Button scheduleCard = boardGrid.Children.OfType<Button>().Single();
            string[] labels = boardGrid.Children
                .OfType<TextBlock>()
                .Select(getTextOrEmpty)
                .ToArray();

            Assert.True(scheduleCard.Bounds.Height >= 24.0);
            Assert.Contains("07:30", labels);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShortRepeatedScheduleUsesCompactUniqueCardsAndAnExportLegend()
    {
        DailyTimeRange timeRange = new DailyTimeRange(
            new ScheduleTime(12, 20),
            new ScheduleTime(12, 35));
        PersonalScheduleDetails details = new PersonalScheduleDetails(
            new PersonalScheduleSection("A"),
            new PersonalScheduleInstructor("김교수"),
            new PersonalScheduleLocation("느헤미야홀 101호"));
        PersonalSchedule schedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("짧은 랩 미팅"),
            new WeeklyTimeRange[]
            {
                new WeeklyTimeRange(EDay.Tuesday, timeRange),
                new WeeklyTimeRange(EDay.Thursday, timeRange),
            },
            details);
        ScheduleEntry[] entries = schedule.TimeRanges
            .Select(range => (ScheduleEntry)new PersonalScheduleEntry(schedule, range))
            .ToArray();
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(
            new ScheduleRecommendation(entries));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 520.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button[] cards = findRequiredControl<Grid>(scheduleBoard, "BoardGrid")
                .Children
                .OfType<Button>()
                .ToArray();
            Assert.Equal(2, cards.Length);
            Assert.All(cards, card => Assert.Contains("compact", card.Classes));
            Assert.All(cards, card => Assert.True(card.Bounds.Height >= 24.0));
            Assert.Equal(
                2,
                cards.Select(AutomationProperties.GetAutomationId).Distinct().Count());
            Assert.All(
                cards,
                card => Assert.Contains(
                    "분반 A",
                    AutomationProperties.GetName(card)));

            string[] exportTexts = scheduleBoard.PngExportSurface
                .GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(getTextOrEmpty)
                .ToArray();
            Assert.Contains("개인 일정 세부 정보", exportTexts);
            Assert.Contains("테스트 계획", exportTexts);
            Assert.Contains("한동대학교 · 2026-2", exportTexts);
            Assert.Contains(
                exportTexts,
                text => text.Contains("분반 A", StringComparison.Ordinal)
                    && text.Contains("김교수", StringComparison.Ordinal)
                    && text.Contains("느헤미야홀 101호", StringComparison.Ordinal));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task UnsatisfiedCourseConstraintsShowAReadOnlyPersonalPreviewAsync()
    {
        using (PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace())
        {
            addPersonalSchedule(
                workspace,
                "월요일 고정 일정",
                EDay.Monday,
                new TimeSpan(8, 30, 0),
                new TimeSpan(9, 45, 0));
            addPersonalSchedule(
                workspace,
                "화요일 고정 일정",
                EDay.Tuesday,
                new TimeSpan(11, 30, 0),
                new TimeSpan(12, 45, 0));

            await workspace.RecommendationRefreshTask;

            Assert.True(workspace.HasUnsatisfiedScheduleConstraints);
            Assert.False(workspace.HasRecommendations);
            Assert.True(workspace.HasScheduleEntries);
            Assert.False(workspace.CanExportSchedule);
            Assert.Empty(workspace.ActiveRecommendation.Entries);
            Assert.NotEmpty(workspace.DisplayedSchedule.Entries);
            Assert.Contains("개인 일정만 미리", workspace.RecommendationInsight);
        }
    }

    private static PersonalSchedule createPersonalSchedule()
    {
        WeeklyTimeRange timeRange = new WeeklyTimeRange(
            EDay.Wednesday,
            new DailyTimeRange(
                new ScheduleTime(12, 20),
                new ScheduleTime(13, 20)));
        PersonalScheduleDetails details = new PersonalScheduleDetails(
            null,
            null,
            new PersonalScheduleLocation("학생회관"));
        return new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("점심 약속"),
            new WeeklyTimeRange[] { timeRange },
            details);
    }

    private static void addPersonalSchedule(
        PlannerWorkspaceViewModel workspace,
        string title,
        EDay day,
        TimeSpan start,
        TimeSpan end)
    {
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        workspace.PersonalScheduleTitleDraft = title;
        setSelectedDay(workspace, day);
        workspace.PersonalScheduleStartTimeOrNull = start;
        workspace.PersonalScheduleEndTimeOrNull = end;
        workspace.SavePersonalScheduleCommand.Execute(null);
    }

    private static void setSelectedDay(
        PlannerWorkspaceViewModel workspace,
        EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                workspace.IsMondaySelected = true;
                break;
            case EDay.Tuesday:
                workspace.IsTuesdaySelected = true;
                break;
            case EDay.Wednesday:
                workspace.IsWednesdaySelected = true;
                break;
            case EDay.Thursday:
                workspace.IsThursdaySelected = true;
                break;
            case EDay.Friday:
                workspace.IsFridaySelected = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "The test supports weekdays only.");
        }
    }

    private static TControl findRequiredControl<TControl>(
        Control root,
        string name)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(name);
        Assert.NotNull(controlOrNull);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException("Required control not found: " + name);
        }

        return controlOrNull;
    }

    private static string getTextOrEmpty(TextBlock textBlock)
    {
        return textBlock.Text == null ? string.Empty : textBlock.Text;
    }

    private static ScheduleBoardPresentation createScheduleBoardPresentation(
        ScheduleRecommendation schedule)
    {
        return new ScheduleBoardPresentation(
            schedule,
            new PlanName("테스트 계획"),
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse("2026-2"));
    }
}
