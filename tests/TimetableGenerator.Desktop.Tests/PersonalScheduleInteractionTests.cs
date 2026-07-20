using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentIcons.Avalonia;
using FluentIcons.Common;

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
            selectPersonalScheduleDay(workspace, EDay.Tuesday);
            selectPersonalScheduleDay(workspace, EDay.Thursday);
            workspace.PersonalScheduleStartTimeOrNull = new ScheduleTime(18, 0);
            workspace.PersonalScheduleEndTimeOrNull = new ScheduleTime(19, 30);
            workspace.PersonalScheduleSectionDraft = "A";
            workspace.PersonalScheduleInstructorDraft = "김교수";
            workspace.PersonalScheduleLocationDraft = "느헤미야홀";

            workspace.SavePersonalScheduleCommand.Execute(null);

            Assert.False(workspace.IsPersonalScheduleEditorVisible);
            PersonalScheduleItem addedItem = Assert.Single(
                workspace.ActivePlan.PersonalSchedules);
            PersonalScheduleId addedScheduleId = addedItem.Id;
            Assert.Equal("랩 미팅", addedItem.Title);
            Assert.Equal("화·목: 18:00–19:30", addedItem.TimeSummary);
            Assert.Equal(
                "분반: A · 담당: 김교수 · 장소: 느헤미야홀",
                addedItem.DetailsSummary);

            workspace.ActivePlan = workspace.Plans[1];
            Assert.Empty(workspace.ActivePlan.PersonalSchedules);

            workspace.ActivePlan = workspace.Plans[0];
            PersonalScheduleItem itemToEdit = Assert.Single(
                workspace.ActivePlan.PersonalSchedules);
            workspace.BeginEditPersonalScheduleCommand.Execute(itemToEdit.Id);
            workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
            workspace.PersonalScheduleLocationDraft = string.Empty;
            workspace.SavePersonalScheduleCommand.Execute(null);

            PersonalScheduleItem editedItem = Assert.Single(
                workspace.ActivePlan.PersonalSchedules);
            Assert.Equal(addedScheduleId, editedItem.Id);
            Assert.Equal("연구실 정기 미팅", editedItem.Title);
            Assert.DoesNotContain("느헤미야홀", editedItem.DetailsSummary);

            workspace.BeginDeletePersonalScheduleCommand.Execute(editedItem);
            Assert.True(workspace.IsDeletePersonalScheduleConfirmationVisible);
            Assert.Equal(
                "시간표에서 개인 일정 '연구실 정기 미팅'을 삭제합니다.",
                workspace.PersonalScheduleDeletionDescription);
            Assert.DoesNotContain(
                "추천 시간표",
                workspace.PersonalScheduleDeletionDescription,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "PNG",
                workspace.PersonalScheduleDeletionDescription,
                StringComparison.Ordinal);
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
            TextBox instructorInput = host.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleInstructorInput");
            Grid rootWorkspaceSurface = findRequiredControl<Grid>(
                host,
                "WorkspaceSurface");

            Assert.True(workspaceSurface.IsVisible);
            Assert.False(rootWorkspaceSurface.IsEnabled);
            Assert.True(nameInput.IsKeyboardFocusWithin);
            Assert.Equal(
                "PersonalScheduleNameInput",
                AutomationProperties.GetAutomationId(nameInput));
            Assert.Equal(
                "개인 일정 담당자",
                AutomationProperties.GetName(instructorInput));
            Assert.Equal(
                VerticalAlignment.Center,
                instructorInput.VerticalContentAlignment);
            Assert.Equal(680.0, dialog.Bounds.Width);
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
            Button closeButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => candidate.Name
                        == "ClosePersonalScheduleEditorButton");
            Assert.Equal(
                "개인 일정 편집기 닫기",
                AutomationProperties.GetName(closeButton));

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
    public void PersonalScheduleDeleteConfirmationUsesACompactCenteredLayout()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.BeginAddPersonalScheduleCommand.Execute(null);
        workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
        selectPersonalScheduleDay(workspace, EDay.Tuesday);
        workspace.SavePersonalScheduleCommand.Execute(null);
        PersonalScheduleItem schedule = Assert.Single(
            workspace.ActivePlan.PersonalSchedules);
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

            Border dialog = findRequiredControl<Border>(
                host,
                "PersonalScheduleEditorDialog");
            Assert.Equal(680.0, dialog.Bounds.Width);

            workspace.CancelPersonalScheduleEditCommand.Execute(null);
            workspace.BeginDeletePersonalScheduleCommand.Execute(schedule);
            Dispatcher.UIThread.RunJobs();

            Border iconSurface = findRequiredControl<Border>(
                host,
                "DeletePersonalScheduleIconSurface");
            TextBlock heading = findRequiredControl<TextBlock>(
                host,
                "DeletePersonalScheduleHeading");
            TextBlock description = findRequiredControl<TextBlock>(
                host,
                "DeletePersonalScheduleDescription");
            StackPanel actions = findRequiredControl<StackPanel>(
                host,
                "DeletePersonalScheduleActions");
            Button cancelButton = findRequiredControl<Button>(
                host,
                "CancelDeletePersonalScheduleButton");
            Button confirmButton = findRequiredControl<Button>(
                host,
                "ConfirmDeletePersonalScheduleButton");

            Assert.Equal(384.0, dialog.MaxWidth);
            Assert.Equal(384.0, dialog.Bounds.Width);
            Assert.Equal(new Thickness(24.0), dialog.Padding);
            Assert.Equal(
                HorizontalAlignment.Center,
                iconSurface.HorizontalAlignment);
            Assert.Equal(TextAlignment.Center, heading.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, heading.TextWrapping);
            Assert.Equal(TextAlignment.Center, description.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
            Assert.Equal("삭제", confirmButton.Content);
            Assert.Equal(
                HorizontalAlignment.Center,
                actions.HorizontalAlignment);
            Assert.All(
                new[] { cancelButton, confirmButton },
                button =>
                {
                    Assert.Equal(
                        HorizontalAlignment.Center,
                        button.HorizontalContentAlignment);
                    Assert.Equal(
                        VerticalAlignment.Center,
                        button.VerticalContentAlignment);
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
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
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
                Control? dayContainerOrNull =
                    dayOptions.ContainerFromIndex(dayIndex);
                if (dayContainerOrNull == null)
                {
                    throw new InvalidOperationException(
                        "The weekday option container was not prepared.");
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
                        Avalonia.Point? originOrNull = candidate.TranslatePoint(
                            new Avalonia.Point(0.0, 0.0),
                            dayOptions);
                        Assert.NotNull(originOrNull);
                        if (originOrNull == null)
                        {
                            throw new InvalidOperationException(
                                "The day option geometry could not be resolved.");
                        }

                        return (
                            Left: originOrNull.Value.X,
                            Width: candidate.Bounds.Width);
                    })
                .OrderBy(geometry => geometry.Left)
                .ToArray();
            double minimumDayWidth = dayInputGeometry.Min(
                geometry => geometry.Width);
            double maximumDayWidth = dayInputGeometry.Max(
                geometry => geometry.Width);
            Assert.InRange(
                maximumDayWidth - minimumDayWidth,
                0.0,
                1.0);
            Assert.All(
                dayInputGeometry,
                geometry =>
                {
                    Assert.True(geometry.Left >= 0.0);
                    Assert.True(
                        geometry.Left + geometry.Width
                            <= dayOptions.Bounds.Width + 0.01);
                });
            double leadingMargin = dayInputGeometry[0].Left;
            (double Left, double Width) lastDay = dayInputGeometry[^1];
            double trailingMargin = dayOptions.Bounds.Width
                - (lastDay.Left + lastDay.Width);
            Assert.InRange(leadingMargin, 0.0, 0.5);
            Assert.InRange(trailingMargin, 0.0, 0.5);
            Assert.InRange(
                Math.Abs(leadingMargin - trailingMargin),
                0.0,
                1.0);
            for (int dayIndex = 0;
                dayIndex < dayInputGeometry.Length - 1;
                ++dayIndex)
            {
                (double Left, double Width) currentDay =
                    dayInputGeometry[dayIndex];
                (double Left, double Width) nextDay =
                    dayInputGeometry[dayIndex + 1];
                double gap = nextDay.Left
                    - (currentDay.Left + currentDay.Width);
                Assert.InRange(gap, 7.0, 9.0);
            }

            TextBox scheduleNameInput = host.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleNameInput");
            Avalonia.Point? dayOptionsPositionOrNull = dayOptions.TranslatePoint(
                new Avalonia.Point(0.0, 0.0),
                host);
            Avalonia.Point? nameInputPositionOrNull =
                scheduleNameInput.TranslatePoint(
                    new Avalonia.Point(0.0, 0.0),
                    host);
            Assert.NotNull(dayOptionsPositionOrNull);
            Assert.NotNull(nameInputPositionOrNull);
            if (dayOptionsPositionOrNull == null
                || nameInputPositionOrNull == null)
            {
                throw new InvalidOperationException(
                    "The personal schedule fields were not attached to the editor.");
            }

            double dayOptionsRight = dayOptionsPositionOrNull.Value.X
                + dayOptions.Bounds.Width;
            double nameInputRight = nameInputPositionOrNull.Value.X
                + scheduleNameInput.Bounds.Width;
            Assert.InRange(
                Math.Abs(
                    dayOptionsPositionOrNull.Value.X
                    - nameInputPositionOrNull.Value.X),
                0.0,
                0.5);
            Assert.InRange(
                Math.Abs(dayOptionsRight - nameInputRight),
                0.0,
                0.5);

            ProductTimePicker startTimeInput = host.GetVisualDescendants()
                .OfType<ProductTimePicker>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleStartTimeInput");
            string?[] segmentNames = startTimeInput.GetVisualDescendants()
                .OfType<ComboBox>()
                .Select(AutomationProperties.GetName)
                .ToArray();
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

            PersonalSchedule savedSchedule = Assert.Single(
                workspace.ActivePlan.Plan.PersonalSchedules);
            Assert.Equal(EDay.Sunday, Assert.Single(savedSchedule.TimeRanges).Day);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void DayOptionsUseCompleteVisualStatesAcrossThemes()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = 900.0;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            PersonalScheduleDayOption mondayOption = workspace
                .PersonalScheduleDayOptions
                .Single(candidate => candidate.Day == EDay.Monday);
            PersonalScheduleDayOption tuesdayOption = workspace
                .PersonalScheduleDayOptions
                .Single(candidate => candidate.Day == EDay.Tuesday);
            ToggleButton mondayInput = host.GetVisualDescendants()
                .OfType<ToggleButton>()
                .Single(candidate => ReferenceEquals(
                    candidate.DataContext,
                    mondayOption));
            ToggleButton tuesdayInput = host.GetVisualDescendants()
                .OfType<ToggleButton>()
                .Single(candidate => ReferenceEquals(
                    candidate.DataContext,
                    tuesdayOption));
            Button closeEditorButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name
                    == "ClosePersonalScheduleEditorButton");
            ThemeVariant[] themeVariants =
            {
                ThemeVariant.Light,
                ThemeVariant.Dark,
            };

            foreach (ThemeVariant themeVariant in themeVariants)
            {
                window.RequestedThemeVariant = themeVariant;
                mondayInput.IsEnabled = true;
                tuesdayInput.IsEnabled = true;
                mondayOption.IsSelected = false;
                tuesdayOption.IsSelected = true;
                movePointerOutsideDayOptions(window);
                Dispatcher.UIThread.RunJobs();

                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "ControlSurfaceBrush",
                    "ControlBorderBrush",
                    new Thickness(1.0));
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionSurfaceBrush",
                    "SelectionIndicatorBrush",
                    new Thickness(1.0));

                Point mondayCenter = findControlCenter(window, mondayInput);
                window.MouseMove(mondayCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "HoverSurfaceBrush",
                    "ControlBorderBrush",
                    new Thickness(1.0));
                window.MouseDown(
                    mondayCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "PressedSurfaceBrush",
                    "ControlBorderBrush",
                    new Thickness(1.0));
                window.MouseUp(
                    mondayCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);

                mondayOption.IsSelected = false;
                movePointerOutsideDayOptions(window);
                Assert.True(closeEditorButton.Focus(NavigationMethod.Tab));
                Assert.True(mondayInput.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "ControlSurfaceBrush",
                    "ProductFocusStrokeBrush",
                    new Thickness(2.0));
                mondayInput.IsEnabled = false;
                window.MouseMove(mondayCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(mondayInput.IsEffectivelyEnabled);
                assertDayOptionVisuals(
                    mondayInput,
                    themeVariant,
                    "ControlSurfaceBrush",
                    "ControlBorderBrush",
                    new Thickness(1.0));
                assertDisabledContentOpacity(mondayInput);

                Point tuesdayCenter = findControlCenter(window, tuesdayInput);
                window.MouseMove(tuesdayCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionHoverSurfaceBrush",
                    "SelectionIndicatorBrush",
                    new Thickness(1.0));
                window.MouseDown(
                    tuesdayCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionPressedSurfaceBrush",
                    "SelectionIndicatorBrush",
                    new Thickness(1.0));
                window.MouseUp(
                    tuesdayCenter,
                    MouseButton.Left,
                    RawInputModifiers.None);

                tuesdayOption.IsSelected = true;
                movePointerOutsideDayOptions(window);
                Assert.True(closeEditorButton.Focus(NavigationMethod.Tab));
                Assert.True(tuesdayInput.Focus(NavigationMethod.Tab));
                Dispatcher.UIThread.RunJobs();
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionSurfaceBrush",
                    "ProductFocusStrokeBrush",
                    new Thickness(2.0));
                tuesdayInput.IsEnabled = false;
                window.MouseMove(tuesdayCenter, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(tuesdayInput.IsEffectivelyEnabled);
                assertDayOptionVisuals(
                    tuesdayInput,
                    themeVariant,
                    "SelectionSurfaceBrush",
                    "SelectionIndicatorBrush",
                    new Thickness(1.0));
                assertDisabledContentOpacity(tuesdayInput);
            }
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
            selectPersonalScheduleDay(workspace, EDay.Wednesday);
            ProductTimePicker startTimeInput = host.GetVisualDescendants()
                .OfType<ProductTimePicker>()
                .Single(
                    candidate => candidate.Name
                        == "PersonalScheduleStartTimeInput");

            startTimeInput.SelectedTimeOrNull = null;
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
            selectPersonalScheduleDay(workspace, EDay.Wednesday);
            workspace.PersonalScheduleEndTimeOrNull = new ScheduleTime(13, 1);
            ProductTimePicker endTimeInput = host.GetVisualDescendants()
                .OfType<ProductTimePicker>()
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
        selectPersonalScheduleDay(workspace, EDay.Tuesday);
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

            workspace.BeginEditPersonalScheduleCommand.Execute(itemToEdit.Id);
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
            TextBlock deleteHeading = findRequiredControl<TextBlock>(
                host,
                "DeletePersonalScheduleHeading");
            Assert.Equal(
                1,
                (int)AutomationProperties.GetHeadingLevel(deleteHeading));
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

            Assert.Equal(17, Grid.GetRow(scheduleCard));
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
    public void PersonalScheduleBoardCardMatchesCourseCardHierarchy()
    {
        DailyTimeRange timeRange = new DailyTimeRange(
            new ScheduleTime(12, 0),
            new ScheduleTime(13, 0));
        PersonalScheduleDetails details = new PersonalScheduleDetails(
            new PersonalScheduleSection("A"),
            new PersonalScheduleInstructor("김교수"),
            new PersonalScheduleLocation("느헤미야홀 101호"));
        PersonalSchedule schedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("사용자 경험 연구 정기 회의"),
            new WeeklyTimeRange[]
            {
                new WeeklyTimeRange(EDay.Wednesday, timeRange),
            },
            details);
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

            Button scheduleCard = findRequiredControl<Grid>(
                scheduleBoard,
                "BoardGrid")
                .Children
                .OfType<Button>()
                .Single();
            Grid cardContent = Assert.IsType<Grid>(scheduleCard.Content);
            TextBlock[] cardTexts = cardContent.Children
                .OfType<TextBlock>()
                .ToArray();
            Assert.Equal(
                new string[] { "사용자 경험 연구 정기 회의", "느헤미야홀 101호", "김교수" },
                cardTexts.Select(getTextOrEmpty));
            Assert.Equal(new Thickness(8.0, 4.0), scheduleCard.Padding);
            Assert.Equal(VerticalAlignment.Center, cardContent.VerticalAlignment);
            Assert.Equal(3, cardContent.RowDefinitions.Count);

            TextBlock title = cardTexts[0];
            Assert.Equal(14.0, title.FontSize);
            Assert.Equal(18.0, title.LineHeight);
            Assert.Equal(FontWeight.Bold, title.FontWeight);
            Assert.Equal(2, title.MaxLines);
            Assert.Equal(TextAlignment.Center, title.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, title.TextWrapping);
            Assert.True(title.Bounds.Height > title.LineHeight);

            double availableContentHeight = scheduleCard.Bounds.Height
                - scheduleCard.Padding.Top
                - scheduleCard.Padding.Bottom
                - scheduleCard.BorderThickness.Top
                - scheduleCard.BorderThickness.Bottom;
            Assert.True(cardContent.DesiredSize.Height <= availableContentHeight);

            TextBlock location = cardTexts[1];
            Assert.Equal(11.5, location.FontSize);
            Assert.Equal(14.0, location.LineHeight);
            Assert.Equal(FontWeight.SemiBold, location.FontWeight);
            Assert.Equal(7.0, location.Margin.Top);
            Assert.Equal(TextAlignment.Center, location.TextAlignment);

            TextBlock responsiblePerson = cardTexts[2];
            Assert.Equal(10.5, responsiblePerson.FontSize);
            Assert.Equal(12.0, responsiblePerson.LineHeight);
            Assert.Equal(FontWeight.Normal, responsiblePerson.FontWeight);
            Assert.Equal(2.0, responsiblePerson.Margin.Top);
            Assert.Equal(TextAlignment.Center, responsiblePerson.TextAlignment);

            string? accessibleNameOrNull = AutomationProperties.GetName(scheduleCard);
            Assert.NotNull(accessibleNameOrNull);
            if (accessibleNameOrNull == null)
            {
                throw new InvalidOperationException(
                    "The personal schedule card accessible name was missing.");
            }

            string accessibleName = accessibleNameOrNull;
            Assert.Contains("분반 A", accessibleName);
            Assert.Contains("수요일 12:00–13:00", accessibleName);
            Assert.Equal(
                "사용자 경험 연구 정기 회의"
                    + Environment.NewLine
                    + "선택하여 개인 일정 상세 정보 보기",
                ToolTip.GetTip(scheduleCard));
            Assert.DoesNotContain(
                cardTexts,
                textBlock => getTextOrEmpty(textBlock).Contains(
                    "개인 일정",
                    StringComparison.Ordinal));
            Assert.DoesNotContain(
                cardTexts,
                textBlock => getTextOrEmpty(textBlock).Contains(
                    "12:00",
                    StringComparison.Ordinal));
            Assert.DoesNotContain(
                cardTexts,
                textBlock => getTextOrEmpty(textBlock).Contains(
                    "분반",
                    StringComparison.Ordinal));

            Button exportCard = scheduleBoard.PngExportSurface
                .GetVisualDescendants()
                .OfType<Button>()
                .Single();
            Grid exportContent = Assert.IsType<Grid>(exportCard.Content);
            TextBlock[] exportCardTexts = exportContent.Children
                .OfType<TextBlock>()
                .ToArray();
            Assert.Equal(
                new string[] { "사용자 경험 연구 정기 회의", "느헤미야홀 101호", "김교수" },
                exportCardTexts.Select(getTextOrEmpty));
            Assert.Equal(2.0, exportCardTexts[2].Margin.Top);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleDetailsOfferPrefilledEditing()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
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
        PersonalScheduleItem personalScheduleItem = Assert.Single(
            workspace.ActivePlan.PersonalSchedules);

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

            string scheduleCardAutomationIdPrefix =
                "PersonalScheduleCard:" + personalScheduleItem.Id;
            Button scheduleCard = host.GetVisualDescendants()
                .OfType<Button>()
                .First(
                    candidate => hasAutomationIdPrefix(
                        candidate,
                        scheduleCardAutomationIdPrefix));
            Flyout detailsFlyout = Assert.IsType<Flyout>(scheduleCard.Flyout);
            detailsFlyout.ShowAt(scheduleCard);
            Dispatcher.UIThread.RunJobs();

            Control detailsContent = Assert.IsAssignableFrom<Control>(
                detailsFlyout.Content);
            Button editButton = detailsContent.GetVisualDescendants()
                .OfType<Button>()
                .Single();
            Assert.Contains("icon", editButton.Classes);
            Assert.Equal(36.0, editButton.Width);
            Assert.Equal(36.0, editButton.Height);
            Assert.Equal(
                "EditPersonalScheduleButton:" + personalScheduleItem.Id,
                AutomationProperties.GetAutomationId(editButton));
            Assert.Equal(
                personalScheduleItem.EditButtonAccessibleName,
                AutomationProperties.GetName(editButton));
            Assert.Equal("개인 일정 수정", ToolTip.GetTip(editButton));
            FluentIcon editIcon = Assert.IsType<FluentIcon>(editButton.Content);
            Assert.Equal(Icon.Edit, editIcon.Icon);
            Assert.Equal(IconVariant.Regular, editIcon.IconVariant);

            ICommand? editCommandOrNull = editButton.Command;
            Assert.NotNull(editCommandOrNull);
            if (editCommandOrNull == null)
            {
                throw new InvalidOperationException(
                    "The personal schedule edit command was missing.");
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
            Assert.Equal(
                new ScheduleTime(20, 0),
                workspace.PersonalScheduleStartTimeOrNull);
            Assert.Equal(
                new ScheduleTime(20, 30),
                workspace.PersonalScheduleEndTimeOrNull);
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
            Assert.Equal(
                new ScheduleBoardTimeBoundary(450),
                scheduleBoard.RenderedLayout.TimeAxis.Start);
            Assert.Equal(
                new ScheduleBoardTimeBoundary(1_140),
                scheduleBoard.RenderedLayout.TimeAxis.End);
            Assert.Equal(138, scheduleBoard.RenderedLayout.TimeAxis.IncrementCount);
            Assert.Equal(23, scheduleBoard.RenderedLayout.TimeAxis.LabelTimes.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ShortRepeatedScheduleUsesCompactUniqueCardsWithoutAnExportLegend()
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
            Assert.All(
                cards,
                card =>
                {
                    TextBlock title = Assert.IsType<TextBlock>(card.Content);
                    Assert.Equal("짧은 랩 미팅", title.Text);
                    Assert.Equal(14.0, title.FontSize);
                    Assert.Equal(18.0, title.LineHeight);
                    Assert.Equal(FontWeight.Bold, title.FontWeight);
                    Assert.Equal(1, title.MaxLines);
                    Assert.Equal(TextAlignment.Center, title.TextAlignment);
                });
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
            Assert.DoesNotContain("개인 일정 세부 정보", exportTexts);
            Assert.Contains("테스트 계획", exportTexts);
            Assert.DoesNotContain("한동대학교 · 2026-2", exportTexts);
            Assert.Equal(
                2,
                exportTexts.Count(text => text == "짧은 랩 미팅"));
            Assert.DoesNotContain(
                exportTexts,
                text => text.Contains("분반 A", StringComparison.Ordinal)
                    || text.Contains("김교수", StringComparison.Ordinal)
                    || text.Contains("느헤미야홀 101호", StringComparison.Ordinal));
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
            Assert.False(workspace.HasRecommendations);
            Assert.True(workspace.HasScheduleEntries);
            Assert.False(workspace.CanExportSchedule);
            Assert.Empty(workspace.ActiveRecommendation.Entries);
            Assert.NotEmpty(workspace.DisplayedSchedule.Entries);
            Assert.True(workspace.HasUnsatisfiedPersonalSchedulePreview);
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

    private static void selectPersonalScheduleDay(
        PlannerWorkspaceViewModel workspace,
        EDay day)
    {
        PersonalScheduleDayOption? matchingOptionOrNull =
            workspace.PersonalScheduleDayOptions.FirstOrDefault(
                option => option.Day == day);
        if (matchingOptionOrNull == null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(day),
                day,
                "The personal schedule day option was not found.");
        }

        matchingOptionOrNull.IsSelected = true;
    }

    private static void assertDayOptionVisuals(
        ToggleButton option,
        ThemeVariant themeVariant,
        string backgroundResourceKey,
        string borderResourceKey,
        Thickness borderThickness)
    {
        assertBrushUsesResource(
            option.Background,
            backgroundResourceKey,
            themeVariant);
        assertBrushUsesResource(
            option.BorderBrush,
            borderResourceKey,
            themeVariant);
        Assert.Equal(borderThickness, option.BorderThickness);
    }

    private static void assertDisabledContentOpacity(ToggleButton option)
    {
        ContentPresenter presenter = option.GetVisualDescendants()
            .OfType<ContentPresenter>()
            .Single(candidate => candidate.Name == "PART_ContentPresenter");
        Assert.Equal(0.45, presenter.Opacity);
    }

    private static void assertBrushUsesResource(
        IBrush? actualBrushOrNull,
        string resourceKey,
        ThemeVariant themeVariant)
    {
        Avalonia.Application? applicationOrNull = Avalonia.Application.Current;
        Assert.NotNull(applicationOrNull);
        if (applicationOrNull == null)
        {
            throw new InvalidOperationException(
                "The Avalonia test application was not initialized.");
        }

        object? resourceOrNull;
        bool hasResource = applicationOrNull.TryGetResource(
            resourceKey,
            themeVariant,
            out resourceOrNull);
        Assert.True(hasResource, "Missing brush resource: " + resourceKey);
        SolidColorBrush actualBrush = Assert.IsType<SolidColorBrush>(
            actualBrushOrNull);
        SolidColorBrush expectedBrush = Assert.IsType<SolidColorBrush>(
            resourceOrNull);
        Assert.Equal(expectedBrush.Color, actualBrush.Color);
    }

    private static Point findControlCenter(Window window, Control control)
    {
        Point? originOrNull = control.TranslatePoint(
            new Point(0.0, 0.0),
            window);
        Assert.NotNull(originOrNull);
        if (originOrNull == null)
        {
            throw new InvalidOperationException(
                "The personal schedule control position could not be resolved.");
        }

        return originOrNull.Value
            + new Vector(control.Bounds.Width / 2.0, control.Bounds.Height / 2.0);
    }

    private static void movePointerOutsideDayOptions(Window window)
    {
        window.MouseMove(new Point(1.0, 1.0), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
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

    private static bool hasAutomationIdPrefix(
        Control control,
        string automationIdPrefix)
    {
        string? automationIdOrNull = AutomationProperties.GetAutomationId(
            control);
        return automationIdOrNull != null
            && automationIdOrNull.StartsWith(
                automationIdPrefix,
                StringComparison.Ordinal);
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
