using System;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentIcons.Avalonia;

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
    public void AddEditAndDeleteStayInsideTheActivePlan()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();

        try
        {
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            workspace.PersonalScheduleTitleDraft = "랩 미팅";
            selectPersonalScheduleDay(workspace, EDay.Tuesday);
            selectPersonalScheduleDay(workspace, EDay.Thursday);
            workspace.PersonalScheduleStartTimeOrNull = new ScheduleTime(18, 0);
            workspace.PersonalScheduleEndTimeOrNull = new ScheduleTime(19, 30);
            workspace.PersonalScheduleSectionDraft = "A";
            workspace.PersonalScheduleInstructorDraft = "김교수";
            workspace.PersonalScheduleLocationDraft = "느헤미야홀";

            workspace.SavePersonalScheduleCommand.Execute(null);

            Assert.False(workspace.IsPersonalScheduleEditorVisible);
            PersonalScheduleItem addedItem = Assert.Single(workspace.ActivePlan.PersonalSchedules);
            PersonalScheduleId addedScheduleId = addedItem.Id;
            Assert.Equal("랩 미팅", addedItem.Title);
            Assert.Equal("랩 미팅(A)", addedItem.TitleDisplayText);
            Assert.Equal("화·목: 18:00–19:30", addedItem.TimeSummary);
            Assert.Equal("담당: 김교수 · 장소: 느헤미야홀", addedItem.DetailsSummary);

            workspace.ActivePlan = workspace.Plans[1];
            Assert.Empty(workspace.ActivePlan.PersonalSchedules);

            workspace.ActivePlan = workspace.Plans[0];
            PersonalScheduleItem itemToEdit = Assert.Single(workspace.ActivePlan.PersonalSchedules);
            workspace.BeginEditPersonalScheduleCommand.Execute(itemToEdit.Id);
            workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
            workspace.PersonalScheduleLocationDraft = string.Empty;
            workspace.SavePersonalScheduleCommand.Execute(null);

            PersonalScheduleItem editedItem = Assert.Single(workspace.ActivePlan.PersonalSchedules);
            Assert.Equal(addedScheduleId, editedItem.Id);
            Assert.Equal("연구실 정기 미팅", editedItem.Title);
            Assert.Equal("연구실 정기 미팅(A)", editedItem.TitleDisplayText);
            Assert.DoesNotContain("느헤미야홀", editedItem.DetailsSummary);

            workspace.BeginDeletePersonalScheduleCommand.Execute(editedItem);
            Assert.True(workspace.IsDeletePersonalScheduleConfirmationVisible);
            Assert.Equal("시간표에서 개인 일정 '연구실 정기 미팅'을 삭제합니다.", workspace.PersonalScheduleDeletionDescription);
            Assert.DoesNotContain("추천 시간표", workspace.PersonalScheduleDeletionDescription, StringComparison.Ordinal);
            Assert.DoesNotContain("PNG", workspace.PersonalScheduleDeletionDescription, StringComparison.Ordinal);
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
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
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

            Border workspaceSurface = findRequiredControl<Border>(host, "PersonalScheduleEditorOverlay");
            Border dialog = findRequiredControl<Border>(host, "PersonalScheduleEditorDialog");
            TextBox nameInput = host.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(candidate => candidate.Name == "PersonalScheduleNameInput");
            TextBox instructorInput = host.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleInstructorInput");
            Grid rootWorkspaceSurface = findRequiredControl<Grid>(host, "WorkspaceSurface");

            Assert.True(workspaceSurface.IsVisible);
            Assert.False(rootWorkspaceSurface.IsEnabled);
            Assert.True(nameInput.IsKeyboardFocusWithin);
            Assert.Equal("PersonalScheduleNameInput", AutomationProperties.GetAutomationId(nameInput));
            Assert.Equal("개인 일정 담당자", AutomationProperties.GetName(instructorInput));
            Assert.Equal(VerticalAlignment.Center, instructorInput.VerticalContentAlignment);
            Assert.Equal(680.0, dialog.Bounds.Width);
            Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(dialog));
            PersonalScheduleEditorView editor = host.GetVisualDescendants().OfType<PersonalScheduleEditorView>().Single();
            Assert.Equal(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation(editor));
            Assert.Equal("개인 일정 대화상자", AutomationProperties.GetName(dialog));
            Button closeButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => candidate.Name
                        == "ClosePersonalScheduleEditorButton");
            Assert.Equal("개인 일정 편집기 닫기", AutomationProperties.GetName(closeButton));

            workspace.SavePersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Border validationSummary = host.GetVisualDescendants()
                .OfType<Border>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleValidationSummary");
            Assert.True(validationSummary.IsVisible);
            Assert.Equal(EPersonalScheduleDraftValidationError.TitleRequired, workspace.PersonalScheduleValidationError);
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
    public void PersonalScheduleDeleteConfirmationUsesACompactCenteredLayout()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
        selectPersonalScheduleDay(workspace, EDay.Tuesday);
        workspace.SavePersonalScheduleCommand.Execute(null);
        PersonalScheduleItem schedule = Assert.Single(workspace.ActivePlan.PersonalSchedules);
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 1200.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            workspace.BeginEditPersonalScheduleCommand.Execute(schedule.Id);
            Dispatcher.UIThread.RunJobs();

            Border dialog = findRequiredControl<Border>(host, "PersonalScheduleEditorDialog");
            Assert.Equal(680.0, dialog.Bounds.Width);

            workspace.CancelPersonalScheduleEditCommand.Execute(null);
            workspace.BeginDeletePersonalScheduleCommand.Execute(schedule);
            Dispatcher.UIThread.RunJobs();

            Border iconSurface = findRequiredControl<Border>(host, "DeletePersonalScheduleIconSurface");
            TextBlock heading = findRequiredControl<TextBlock>(host, "DeletePersonalScheduleHeading");
            TextBlock description = findRequiredControl<TextBlock>(host, "DeletePersonalScheduleDescription");
            StackPanel actions = findRequiredControl<StackPanel>(host, "DeletePersonalScheduleActions");
            Button cancelButton = findRequiredControl<Button>(host, "CancelDeletePersonalScheduleButton");
            Button confirmButton = findRequiredControl<Button>(host, "ConfirmDeletePersonalScheduleButton");

            Assert.Equal(384.0, dialog.MaxWidth);
            Assert.Equal(384.0, dialog.Bounds.Width);
            Assert.Equal(new Thickness(24.0), dialog.Padding);
            Assert.Equal(HorizontalAlignment.Center, iconSurface.HorizontalAlignment);
            Assert.Equal(TextAlignment.Center, heading.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, heading.TextWrapping);
            Assert.Equal(TextAlignment.Center, description.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
            Assert.Equal("삭제", confirmButton.Content);
            Assert.Equal(HorizontalAlignment.Center, actions.HorizontalAlignment);
            Assert.All(
                new[] { cancelButton, confirmButton },
                button =>
                {
                    Assert.Equal(HorizontalAlignment.Center, button.HorizontalContentAlignment);
                    Assert.Equal(VerticalAlignment.Center, button.VerticalContentAlignment);
                });
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void EditorSupportsTheWholeWeekAndUsesLocalizedTimeOrder()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 695.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            ToggleButton[] dayInputs = host.GetVisualDescendants()
                .OfType<ToggleButton>()
                .Where(
                    candidate => candidate.DataContext
                        is PersonalScheduleDayOption)
                .ToArray();
            Assert.Equal(7, dayInputs.Length);
            ItemsControl dayOptions = dayInputs[0]
                .GetVisualAncestors()
                .OfType<ItemsControl>()
                .Single(
                    candidate => AutomationProperties.GetName(candidate)
                        == "요일 선택");
            Assert.Contains(
                dayInputs,
                candidate => AutomationProperties.GetAutomationId(candidate)
                    == "PersonalScheduleSaturdayInput");
            Assert.Contains(
                dayInputs,
                candidate => AutomationProperties.GetAutomationId(candidate)
                    == "PersonalScheduleSundayInput");
            Grid dayOptionsGrid = Assert.IsType<Grid>(dayOptions.ItemsPanelRoot);
            Assert.Equal(7, dayOptionsGrid.ColumnDefinitions.Count);
            Assert.All(
                dayOptionsGrid.ColumnDefinitions,
                columnDefinition => Assert.Equal(
                    GridUnitType.Star,
                    columnDefinition.Width.GridUnitType));
            Assert.Equal(8.0, dayOptionsGrid.ColumnSpacing);
            for (int dayIndex = 0; dayIndex < dayInputs.Length; ++dayIndex)
            {
                Control? dayContainerOrNull = dayOptions.ContainerFromIndex(dayIndex);
                if (dayContainerOrNull == null)
                {
                    throw new InvalidOperationException("The weekday option container was not prepared.");
                }

                Assert.Equal(dayIndex, Grid.GetColumn(dayContainerOrNull));
            }

            Assert.All(
                dayInputs,
                candidate => Assert.True(candidate.Bounds.Width >= 76.0));
            Assert.All(
                dayInputs,
                candidate => Assert.Equal(
                    VerticalAlignment.Center,
                    candidate.VerticalContentAlignment));
            FluentIcon[] selectionIndicators = dayInputs
                .SelectMany(
                    candidate => candidate.GetVisualDescendants()
                        .OfType<FluentIcon>())
                .Where(
                    candidate => candidate.Classes.Contains(
                        "day-selection-indicator"))
                .ToArray();
            Assert.Equal(7, selectionIndicators.Length);
            Assert.All(
                selectionIndicators,
                candidate => Assert.False(candidate.IsVisible));
            PersonalScheduleDayOption mondayOption = workspace
                .PersonalScheduleDayOptions
                .Single(candidate => candidate.Day == EDay.Monday);
            mondayOption.IsSelected = true;
            Dispatcher.UIThread.RunJobs();
            ToggleButton mondayInput = dayInputs.Single(
                candidate => ReferenceEquals(candidate.DataContext, mondayOption));
            FluentIcon mondaySelectionIndicator = mondayInput
                .GetVisualDescendants()
                .OfType<FluentIcon>()
                .Single(
                    candidate => candidate.Classes.Contains(
                        "day-selection-indicator"));
            Assert.Equal(true, mondayInput.IsChecked);
            Assert.True(mondaySelectionIndicator.IsVisible);
            Assert.All(
                selectionIndicators.Where(
                    candidate => ReferenceEquals(
                        candidate,
                        mondaySelectionIndicator) == false),
                candidate => Assert.False(candidate.IsVisible));
            mondayOption.IsSelected = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(false, mondayInput.IsChecked);
            Assert.False(mondaySelectionIndicator.IsVisible);
            (double Left, double Width)[] dayInputGeometry = dayInputs
                .Select(
                    candidate =>
                    {
                        Avalonia.Point? originOrNull = candidate.TranslatePoint(new Avalonia.Point(0.0, 0.0), dayOptions);
                        Assert.NotNull(originOrNull);
                        if (originOrNull == null)
                        {
                            throw new InvalidOperationException("The day option geometry could not be resolved.");
                        }

                        return (Left: originOrNull.Value.X, Width: candidate.Bounds.Width);
                    })
                .OrderBy(geometry => geometry.Left)
                .ToArray();
            double minimumDayWidth = dayInputGeometry.Min(
                geometry => geometry.Width);
            double maximumDayWidth = dayInputGeometry.Max(
                geometry => geometry.Width);
            Assert.InRange(maximumDayWidth - minimumDayWidth, 0.0, 1.0);
            Assert.All(
                dayInputGeometry,
                geometry =>
                {
                    Assert.True(geometry.Left >= 0.0);
                    Assert.True(geometry.Left + geometry.Width <= dayOptions.Bounds.Width + 0.01);
                });
            double leadingMargin = dayInputGeometry[0].Left;
            (double Left, double Width) lastDay = dayInputGeometry[^1];
            double trailingMargin = dayOptions.Bounds.Width - (lastDay.Left + lastDay.Width);
            Assert.InRange(leadingMargin, 0.0, 0.5);
            Assert.InRange(trailingMargin, 0.0, 0.5);
            Assert.InRange(Math.Abs(leadingMargin - trailingMargin), 0.0, 1.0);
            for (int dayIndex = 0; dayIndex < dayInputGeometry.Length - 1; ++dayIndex)
            {
                (double Left, double Width) currentDay = dayInputGeometry[dayIndex];
                (double Left, double Width) nextDay = dayInputGeometry[dayIndex + 1];
                double gap = nextDay.Left - (currentDay.Left + currentDay.Width);
                Assert.InRange(gap, 7.0, 9.0);
            }

            TextBox scheduleNameInput = host.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleNameInput");
            Avalonia.Point? dayOptionsPositionOrNull = dayOptions.TranslatePoint(new Avalonia.Point(0.0, 0.0), host);
            Avalonia.Point? nameInputPositionOrNull = scheduleNameInput.TranslatePoint(new Avalonia.Point(0.0, 0.0), host);
            Assert.NotNull(dayOptionsPositionOrNull);
            Assert.NotNull(nameInputPositionOrNull);
            if (dayOptionsPositionOrNull == null || nameInputPositionOrNull == null)
            {
                throw new InvalidOperationException("The personal schedule fields were not attached to the editor.");
            }

            double dayOptionsRight = dayOptionsPositionOrNull.Value.X + dayOptions.Bounds.Width;
            double nameInputRight = nameInputPositionOrNull.Value.X + scheduleNameInput.Bounds.Width;
            Assert.InRange(Math.Abs(dayOptionsPositionOrNull.Value.X - nameInputPositionOrNull.Value.X), 0.0, 0.5);
            Assert.InRange(Math.Abs(dayOptionsRight - nameInputRight), 0.0, 0.5);

            ProductTimePicker startTimeInput = host.GetVisualDescendants()
                .OfType<ProductTimePicker>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleStartTimeInput");
            string?[] segmentNames = startTimeInput.GetVisualDescendants().OfType<ComboBox>().Select(AutomationProperties.GetName).ToArray();
            Assert.Equal(
                new string?[]
                {
                    "시작 시간 오전 또는 오후",
                    "시작 시간 시",
                    "시작 시간 분",
                },
                segmentNames);

            workspace.PersonalScheduleTitleDraft = "주말 랩 미팅";
            selectPersonalScheduleDay(workspace, EDay.Sunday);
            workspace.SavePersonalScheduleCommand.Execute(null);

            PersonalSchedule savedSchedule = Assert.Single(workspace.ActivePlan.Plan.PersonalSchedules);
            Assert.Equal(EDay.Sunday, Assert.Single(savedSchedule.TimeRanges).Day);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

}
