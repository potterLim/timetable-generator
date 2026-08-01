using System.Linq;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class PersonalScheduleInteractionTests
{
    [AvaloniaFact]
    public void ClearedRequiredTimeIsRejectedAndFocused()
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
            Assert.Equal(EPersonalScheduleDraftValidationError.StartTimeRequired, workspace.PersonalScheduleValidationError);
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

            Assert.Equal(EPersonalScheduleDraftValidationError.EndTimePrecisionInvalid, workspace.PersonalScheduleValidationError);
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
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
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
            PersonalScheduleItem itemToEdit = Assert.Single(workspace.ActivePlan.PersonalSchedules);
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

            PersonalScheduleItem itemToDelete = Assert.Single(workspace.ActivePlan.PersonalSchedules);
            Button deleteButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => AutomationProperties.GetName(candidate)
                        == itemToDelete.RemoveButtonAccessibleName);
            Assert.True(deleteButton.Focus());

            workspace.BeginDeletePersonalScheduleCommand.Execute(itemToDelete);
            Dispatcher.UIThread.RunJobs();
            TextBlock deleteHeading = findRequiredControl<TextBlock>(host, "DeletePersonalScheduleHeading");
            Assert.Equal(1, (int)AutomationProperties.GetHeadingLevel(deleteHeading));
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
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
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
            Assert.Equal(EPersonalScheduleDraftValidationError.TitleRequired, workspace.PersonalScheduleValidationError);
            Assert.True(nameInput.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

}
