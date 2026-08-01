using System;
using System.Linq;
using System.Windows.Input;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentIcons.Avalonia;
using FluentIcons.Common;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class PersonalScheduleInteractionTests
{
    [AvaloniaFact]
    public void PersonalScheduleDetailsOfferPrefilledEditing()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        workspace.PersonalScheduleTitleDraft = "채플특강 받고";
        selectPersonalScheduleDay(workspace, EDay.Thursday);
        selectPersonalScheduleDay(workspace, EDay.Saturday);
        workspace.PersonalScheduleStartTimeOrNull = new ScheduleTime(20, 0);
        workspace.PersonalScheduleEndTimeOrNull = new ScheduleTime(20, 30);
        workspace.PersonalScheduleSectionDraft = "B";
        workspace.PersonalScheduleInstructorDraft = "담당자";
        workspace.PersonalScheduleLocationDraft = "오석관";
        workspace.SavePersonalScheduleCommand.Execute(null);
        PersonalScheduleItem personalScheduleItem = Assert.Single(workspace.ActivePlan.PersonalSchedules);

        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1_440.0;
        window.Height = 900.0;
        window.Content = host;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            string scheduleCardAutomationIdPrefix = "PersonalScheduleCard:" + personalScheduleItem.Id;
            Button scheduleCard = host.GetVisualDescendants()
                .OfType<Button>()
                .First(
                    candidate => hasAutomationIdPrefix(
                        candidate,
                        scheduleCardAutomationIdPrefix));
            Flyout detailsFlyout = Assert.IsType<Flyout>(scheduleCard.Flyout);
            detailsFlyout.ShowAt(scheduleCard);
            Dispatcher.UIThread.RunJobs();

            Control detailsContent = Assert.IsAssignableFrom<Control>(detailsFlyout.Content);
            Button editButton = detailsContent.GetVisualDescendants().OfType<Button>().Single();
            Assert.Contains("icon", editButton.Classes);
            Assert.Equal(36.0, editButton.Width);
            Assert.Equal(36.0, editButton.Height);
            Assert.Equal("EditPersonalScheduleButton:" + personalScheduleItem.Id, AutomationProperties.GetAutomationId(editButton));
            Assert.Equal(personalScheduleItem.EditButtonAccessibleName, AutomationProperties.GetName(editButton));
            Assert.Equal("개인 일정 수정", ToolTip.GetTip(editButton));
            FluentIcon editIcon = Assert.IsType<FluentIcon>(editButton.Content);
            Assert.Equal(Icon.Edit, editIcon.Icon);
            Assert.Equal(IconVariant.Regular, editIcon.IconVariant);

            ICommand? editCommandOrNull = editButton.Command;
            Assert.NotNull(editCommandOrNull);
            if (editCommandOrNull == null)
            {
                throw new InvalidOperationException("The personal schedule edit command was missing.");
            }

            editCommandOrNull.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(detailsFlyout.IsOpen);
            Assert.True(workspace.IsPersonalScheduleEditorVisible);
            Assert.Equal("개인 일정 수정", workspace.PersonalScheduleEditorHeading);
            Assert.Equal("채플특강 받고", workspace.PersonalScheduleTitleDraft);
            Assert.Equal("B", workspace.PersonalScheduleSectionDraft);
            Assert.Equal("담당자", workspace.PersonalScheduleInstructorDraft);
            Assert.Equal("오석관", workspace.PersonalScheduleLocationDraft);
            Assert.Equal(new ScheduleTime(20, 0), workspace.PersonalScheduleStartTimeOrNull);
            Assert.Equal(new ScheduleTime(20, 30), workspace.PersonalScheduleEndTimeOrNull);
            Assert.Equal(
                new EDay[] { EDay.Thursday, EDay.Saturday },
                workspace.PersonalScheduleDayOptions
                    .Where(option => option.IsSelected)
                    .Select(option => option.Day));
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleDetailsStayInsideTheWindowAcrossWeekdayAndWeekendColumns()
    {
        DailyTimeRange timeRange = new DailyTimeRange(new ScheduleTime(20, 0), new ScheduleTime(21, 0));
        PersonalSchedule schedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("저녁 고정 일정"),
            new WeeklyTimeRange[]
            {
                new WeeklyTimeRange(EDay.Monday, timeRange),
                new WeeklyTimeRange(EDay.Saturday, timeRange),
                new WeeklyTimeRange(EDay.Sunday, timeRange),
            },
            PersonalScheduleDetails.CreateEmpty());
        ScheduleEntry[] entries = schedule.TimeRanges
            .Select(range => (ScheduleEntry)new PersonalScheduleEntry(schedule, range))
            .ToArray();
        ScheduleBoardView scheduleBoard = new ScheduleBoardView();
        scheduleBoard.DataContext = createScheduleBoardPresentation(new ScheduleRecommendation(entries));
        Window window = new Window();
        window.Width = 800.0;
        window.Height = 520.0;
        window.Content = scheduleBoard;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button[] cards = findRequiredControl<Grid>(scheduleBoard, "BoardGrid").Children.OfType<Button>().ToArray();
            Assert.Equal(3, cards.Length);
            assertDetailsFlyoutFitsWindow(cards[0], PlacementMode.BottomEdgeAlignedLeft, window);
            assertDetailsFlyoutFitsWindow(cards[1], PlacementMode.BottomEdgeAlignedRight, window);
            assertDetailsFlyoutFitsWindow(cards[2], PlacementMode.BottomEdgeAlignedRight, window);
        }
        finally
        {
            window.Close();
        }
    }

}
