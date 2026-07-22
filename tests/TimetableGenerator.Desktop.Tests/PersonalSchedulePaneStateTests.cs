using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PersonalSchedulePaneStateTests
{
    [AvaloniaFact]
    public void TopScheduleCommandKeepsTheInspectorClosedAfterSaveAndCancel()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();

        try
        {
            workspace.IsInspectorPaneOpen = false;
            workspace.BeginAddPersonalScheduleCommand.Execute(null);

            Assert.False(workspace.IsInspectorPaneOpen);

            workspace.PersonalScheduleTitleDraft = "랩 미팅";
            selectPersonalScheduleDay(workspace, EDay.Tuesday);
            workspace.SavePersonalScheduleCommand.Execute(null);

            Assert.False(workspace.IsInspectorPaneOpen);

            PersonalScheduleItem schedule = Assert.Single(workspace.ActivePlan.PersonalSchedules);
            workspace.BeginEditPersonalScheduleCommand.Execute(schedule.Id);
            workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
            workspace.IsInspectorPaneOpen = true;
            workspace.SavePersonalScheduleCommand.Execute(null);

            Assert.False(workspace.IsInspectorPaneOpen);

            schedule = Assert.Single(workspace.ActivePlan.PersonalSchedules);
            workspace.BeginEditPersonalScheduleCommand.Execute(schedule.Id);
            workspace.IsInspectorPaneOpen = true;
            workspace.CancelPersonalScheduleEditCommand.Execute(null);

            Assert.False(workspace.IsInspectorPaneOpen);

            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            workspace.IsInspectorPaneOpen = true;
            workspace.CancelPersonalScheduleEditCommand.Execute(null);

            Assert.False(workspace.IsInspectorPaneOpen);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void InspectorScheduleCommandsRestoreTheOpenPaneAfterSaveAndCancel()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();

        try
        {
            workspace.IsInspectorPaneOpen = true;
            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            workspace.IsInspectorPaneOpen = false;
            workspace.CancelPersonalScheduleEditCommand.Execute(null);

            Assert.True(workspace.IsInspectorPaneOpen);

            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            workspace.PersonalScheduleTitleDraft = "랩 미팅";
            selectPersonalScheduleDay(workspace, EDay.Tuesday);
            workspace.IsInspectorPaneOpen = false;
            workspace.SavePersonalScheduleCommand.Execute(null);

            Assert.True(workspace.IsInspectorPaneOpen);

            PersonalScheduleItem schedule = Assert.Single(workspace.ActivePlan.PersonalSchedules);
            workspace.BeginEditPersonalScheduleCommand.Execute(schedule.Id);
            workspace.PersonalScheduleTitleDraft = "연구실 정기 미팅";
            workspace.IsInspectorPaneOpen = false;
            workspace.SavePersonalScheduleCommand.Execute(null);

            Assert.True(workspace.IsInspectorPaneOpen);

            schedule = Assert.Single(workspace.ActivePlan.PersonalSchedules);
            workspace.BeginEditPersonalScheduleCommand.Execute(schedule.Id);
            workspace.IsInspectorPaneOpen = false;
            workspace.CancelPersonalScheduleEditCommand.Execute(null);

            Assert.True(workspace.IsInspectorPaneOpen);

            workspace.BeginAddPersonalScheduleCommand.Execute(null);
            workspace.IsInspectorPaneOpen = false;
            workspace.closeTransientWorkspaceOverlays();

            Assert.True(workspace.IsInspectorPaneOpen);
        }
        finally
        {
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PersonalScheduleEditorPreservesInspectorAtOverlayWidth()
    {
        const double MEDIUM_WORKSPACE_WIDTH = 1_080.0;

        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(MEDIUM_WORKSPACE_WIDTH));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = new Window();
        window.Width = MEDIUM_WORKSPACE_WIDTH;
        window.Height = 760.0;
        window.Content = host;

        try
        {
            window.Show();
            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Border inspectorPaneHost = host.GetVisualDescendants()
                .OfType<Border>()
                .Single(candidate => candidate.Name == "InspectorPaneHost");
            Button inspectorAddButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => candidate.Name == "AddPersonalScheduleButton");

            Assert.Contains("overlay", inspectorPaneHost.Classes);
            Assert.True(workspace.IsInspectorPaneOpen);
            Assert.True(inspectorPaneHost.IsEffectivelyVisible);
            Assert.True(inspectorAddButton.Focus());

            clickButton(window, inspectorAddButton);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsInspectorPaneOpen);
            Assert.True(inspectorPaneHost.IsEffectivelyVisible);
            Assert.DoesNotContain(
                host.GetVisualDescendants().OfType<TextBlock>(),
                candidate => getTextOrEmpty(candidate).Contains(
                    "시간표에만 반영됩니다",
                    StringComparison.Ordinal));

            workspace.CancelPersonalScheduleEditCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsInspectorPaneOpen);
            Assert.True(inspectorPaneHost.IsEffectivelyVisible);
            Assert.True(inspectorAddButton.IsKeyboardFocusWithin);

            inspectorAddButton.Command?.Execute(null);
            workspace.PersonalScheduleTitleDraft = "랩 미팅";
            selectPersonalScheduleDay(workspace, EDay.Tuesday);
            Dispatcher.UIThread.RunJobs();

            Button saveButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => candidate.Name == "SavePersonalScheduleButton");
            clickButton(window, saveButton);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsPersonalScheduleEditorVisible);
            Assert.True(workspace.IsInspectorPaneOpen);
            Assert.True(inspectorPaneHost.IsEffectivelyVisible);
            Assert.Single(workspace.ActivePlan.PersonalSchedules);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static void selectPersonalScheduleDay(PlannerWorkspaceViewModel workspace, EDay day)
    {
        PersonalScheduleDayOption? matchingOptionOrNull =
            workspace.PersonalScheduleDayOptions.FirstOrDefault(
                candidate => candidate.Day == day);
        if (matchingOptionOrNull == null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(day),
                day,
                "The personal schedule day option was not found.");
        }

        matchingOptionOrNull.IsSelected = true;
    }

    private static string getTextOrEmpty(TextBlock textBlock)
    {
        if (textBlock.Text == null)
        {
            return string.Empty;
        }

        return textBlock.Text;
    }

    private static void clickButton(Window window, Button button)
    {
        Point? buttonPositionOrNull = button.TranslatePoint(new Point(0.0, 0.0), window);
        if (buttonPositionOrNull.HasValue == false)
        {
            throw new InvalidOperationException("The button could not be translated to the test window.");
        }

        Point buttonPosition = buttonPositionOrNull.Value;
        Point clickPosition = new Point(
            buttonPosition.X + (button.Bounds.Width / 2.0),
            buttonPosition.Y + (button.Bounds.Height / 2.0));
        window.MouseMove(clickPosition, RawInputModifiers.None);
        window.MouseDown(clickPosition, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(clickPosition, MouseButton.Left, RawInputModifiers.None);
    }
}
