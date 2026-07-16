using System;
using System.Linq;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
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
            workspace.PersonalScheduleStartTime = new TimeSpan(18, 0, 0);
            workspace.PersonalScheduleEndTime = new TimeSpan(19, 30, 0);
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

            workspace.CancelPersonalScheduleEditCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspaceSurface.IsVisible);
            Assert.True(rootWorkspaceSurface.IsEnabled);
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
        scheduleBoard.DataContext = new ScheduleRecommendation(
            new ScheduleEntry[] { entry });
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
}
