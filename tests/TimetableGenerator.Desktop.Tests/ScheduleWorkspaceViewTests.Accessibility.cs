using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ScheduleWorkspaceViewTests
{
    [AvaloniaFact]
    public async Task ScheduleCanBeReadAsAnOrderedAccessibleListAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        ScheduleWorkspaceView workspaceView = new ScheduleWorkspaceView();
        workspaceView.DataContext = workspace;

        Window window = new Window();
        window.Width = 1_100.0;
        window.Height = 720.0;
        window.Content = workspaceView;

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ScheduleBoardView? boardOrNull = workspaceView.FindControl<ScheduleBoardView>("ScheduleBoard");
            Border? listOrNull = workspaceView.FindControl<Border>("ScheduleListContainer");
            Button? modeButtonOrNull = workspaceView.FindControl<Button>("ScheduleViewModeButton");
            TextBlock? modeTextOrNull = workspaceView.FindControl<TextBlock>("ScheduleViewModeText");
            ListBox? listItemsOrNull = workspaceView.FindControl<ListBox>("ScheduleListItems");
            Assert.NotNull(boardOrNull);
            Assert.NotNull(listOrNull);
            Assert.NotNull(modeButtonOrNull);
            Assert.NotNull(modeTextOrNull);
            Assert.NotNull(listItemsOrNull);
            if (boardOrNull == null
                || listOrNull == null
                || modeButtonOrNull == null
                || modeTextOrNull == null
                || listItemsOrNull == null)
            {
                throw new InvalidOperationException("The schedule presentation controls were not found.");
            }

            Assert.True(boardOrNull.IsVisible);
            Assert.False(listOrNull.IsVisible);

            workspaceView.ToggleSchedulePresentationCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(boardOrNull.IsVisible);
            Assert.True(listOrNull.IsVisible);
            Assert.Equal("주간 시간표", modeTextOrNull.Text);
            Assert.Equal("주간 시간표로 보기", AutomationProperties.GetName(modeButtonOrNull));
            Assert.Equal("주간 시간표로 보기", ToolTip.GetTip(modeButtonOrNull));
            Assert.Equal(workspace.DisplayedScheduleBoard!.ListGroups.Count, listItemsOrNull.ItemCount);
            ListBoxItem semanticListEntry = listItemsOrNull.GetVisualDescendants().OfType<ListBoxItem>().First();
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(semanticListEntry)));
            IReadOnlyList<string> visibleListText = listOrNull
                .GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(candidate =>
                {
                    if (candidate.Text is string text)
                    {
                        return text;
                    }

                    return string.Empty;
                })
                .ToList();
            Assert.DoesNotContain("과목", visibleListText);
            Assert.DoesNotContain("장소", visibleListText);
            Assert.DoesNotContain("담당", visibleListText);

            ScheduleListOccurrence occurrenceWithMetadata = workspace
                .DisplayedScheduleBoard!
                .ListGroups
                .SelectMany(group => group.Occurrences)
                .First(occurrence => occurrence.HasMetadata);
            TextBlock occurrenceSchedule = listOrNull
                .GetVisualDescendants()
                .OfType<TextBlock>()
                .First(
                    candidate => candidate.Text
                        == occurrenceWithMetadata.ScheduleDisplayText);
            TextBlock occurrenceMetadata = listOrNull
                .GetVisualDescendants()
                .OfType<TextBlock>()
                .First(
                    candidate => candidate.Text
                        == occurrenceWithMetadata.MetadataDisplayText);
            ItemsControl occurrenceList = occurrenceSchedule.GetVisualAncestors().OfType<ItemsControl>().First();
            Assert.Equal(VerticalAlignment.Center, occurrenceList.VerticalAlignment);
            Assert.Equal(20.0, occurrenceSchedule.LineHeight);
            Assert.Equal(20.0, occurrenceMetadata.LineHeight);

            workspaceView.ToggleSchedulePresentationCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(boardOrNull.IsVisible);
            Assert.False(listOrNull.IsVisible);
            Assert.Equal("일정 목록", modeTextOrNull.Text);
            Assert.Equal("일정 목록으로 보기", AutomationProperties.GetName(modeButtonOrNull));
            Assert.Equal("일정 목록으로 보기", ToolTip.GetTip(modeButtonOrNull));
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void ScheduleListGroupsAreSortedAndOmitUnavailableMetadata()
    {
        ScheduleEntry fridayEntry = createScheduleEntry(EDay.Friday, new AcademicPeriod(3));
        ScheduleEntry mondayEntry = createMetadataAvailabilityEntry(
            new CourseCode("TST00101"),
            new KoreanCourseName("정보 없는 수업"),
            EDay.Monday,
            new ScheduleInstructorSummary(
                InstructorAssignmentMetadata.Unconfirmed),
            new ScheduleLocationSummary(LocationAssignmentMetadata.NotProvided));
        ScheduleBoardPresentation presentation = createScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { fridayEntry, mondayEntry }));

        Assert.Equal(2, presentation.ListGroups.Count);
        ScheduleListGroup firstGroup = presentation.ListGroups[0];
        ScheduleListOccurrence firstOccurrence = Assert.Single(firstGroup.Occurrences);
        Assert.Equal(EDay.Monday, firstGroup.EarliestDay);
        Assert.Equal("정보 없는 수업(01)", firstGroup.TitleDisplayText);
        Assert.False(firstOccurrence.HasLocation);
        Assert.False(firstOccurrence.HasResponsiblePerson);
        Assert.DoesNotContain("장소", firstGroup.AccessibleName);
        Assert.DoesNotContain("담당", firstGroup.AccessibleName);
        Assert.Equal(EDay.Friday, presentation.ListGroups[1].EarliestDay);
    }

    [AvaloniaFact]
    public void AcademicPeriodRejectsValuesOutsideSupportedRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new AcademicPeriod(0);
            });
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                new AcademicPeriod(11);
            });
    }

}
