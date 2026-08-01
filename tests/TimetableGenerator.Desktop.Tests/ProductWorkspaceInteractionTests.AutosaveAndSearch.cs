using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Desktop.Tests.Storage;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ProductWorkspaceInteractionTests
{
    [AvaloniaFact]
    public async Task FastAutosaveCompletesWithoutShowingAStatusAsync()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_280.0);

        try
        {
            await workspace.RecommendationRefreshTask;
            window.Show();
            Dispatcher.UIThread.RunJobs();
            StackPanel savingStatus = findRequiredControl<StackPanel>(host, "WorkspaceAutosaveSavingStatus");

            workspace.BeginRenamePlanCommand.Execute(null);
            workspace.PlanNameDraft = "빠른 자동 저장 확인";
            workspace.ConfirmPlanNameCommand.Execute(null);
            await workspace.FlushAutosaveAsync(CancellationToken.None);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(AUTOSAVE_INDICATOR_REVEAL_WAIT, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(EPlanningWorkspaceAutosaveStatus.Saved, workspace.AutosaveStatus);
            Assert.Equal(string.Empty, workspace.AutosaveStatusText);
            Assert.False(workspace.IsAutosaveSaving);
            Assert.False(savingStatus.IsVisible);
            Assert.DoesNotContain(host.GetVisualDescendants().OfType<TextBlock>(), textBlock => textBlock.Text == "자동 저장됨");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task AutosaveSavingIndicatorAppearsOnlyForLongRunningSavesAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt saveAttempt = new ControlledSaveAttempt();
        ControlledSaveAttempt followupSaveAttempt = new ControlledSaveAttempt();
        followupSaveAttempt.CompleteSuccessfully();
        store.EnqueueSaveAttempt(saveAttempt);
        store.EnqueueSaveAttempt(followupSaveAttempt);
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(store);
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_280.0);

        try
        {
            await workspace.RecommendationRefreshTask;
            window.Show();
            Dispatcher.UIThread.RunJobs();
            StackPanel savingStatus = findRequiredControl<StackPanel>(host, "WorkspaceAutosaveSavingStatus");

            Assert.False(workspace.IsAutosaveSaving);
            Assert.False(savingStatus.IsVisible);
            Assert.DoesNotContain(host.GetVisualDescendants().OfType<TextBlock>(), textBlock => textBlock.Text == "자동 저장됨");

            workspace.BeginRenamePlanCommand.Execute(null);
            workspace.PlanNameDraft = "느린 자동 저장 확인";
            workspace.ConfirmPlanNameCommand.Execute(null);
            await saveAttempt.WaitForStartAsync();
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(EPlanningWorkspaceAutosaveStatus.Saving, workspace.AutosaveStatus);
            Assert.False(workspace.IsAutosaveSaving);
            Assert.False(savingStatus.IsVisible);

            await Task.Delay(AUTOSAVE_INDICATOR_REVEAL_WAIT, TestContext.Current.CancellationToken);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsAutosaveSaving);
            Assert.True(savingStatus.IsEffectivelyVisible);
            Assert.Contains(savingStatus.GetVisualDescendants().OfType<TextBlock>(), textBlock => textBlock.Text == "저장 중...");

            saveAttempt.CompleteSuccessfully();
            await workspace.FlushAutosaveAsync(CancellationToken.None);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(EPlanningWorkspaceAutosaveStatus.Saved, workspace.AutosaveStatus);
            Assert.Equal(string.Empty, workspace.AutosaveStatusText);
            Assert.False(workspace.IsAutosaveSaving);
            Assert.False(savingStatus.IsVisible);
            Assert.DoesNotContain(host.GetVisualDescendants().OfType<TextBlock>(), textBlock => textBlock.Text == "자동 저장됨");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task EmptyWorkspaceKeepsAutosaveFailureRecoveryVisibleAsync()
    {
        ControlledPlanningWorkspaceStore store = new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt creationSaveAttempt = new ControlledSaveAttempt();
        ControlledSaveAttempt deletionSaveAttempt = new ControlledSaveAttempt();
        ControlledSaveAttempt retrySaveAttempt = new ControlledSaveAttempt();
        store.EnqueueSaveAttempt(creationSaveAttempt);
        store.EnqueueSaveAttempt(deletionSaveAttempt);
        store.EnqueueSaveAttempt(retrySaveAttempt);
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspaceWithoutPlans(store);
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_280.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(host.GetVisualDescendants().OfType<TextBlock>(), textBlock => textBlock.Text == "자동 저장됨");
            Button createFirstPlanButton = findRequiredControl<Button>(host, "CreateFirstPlanButton");
            createFirstPlanButton.Command?.Execute(null);
            workspace.ConfirmPlanNameCommand.Execute(null);
            await creationSaveAttempt.WaitForStartAsync();
            creationSaveAttempt.CompleteSuccessfully();
            await workspace.FlushAutosaveAsync(CancellationToken.None);

            workspace.ActivePlan.CloseCommand.Execute(null);
            workspace.ConfirmDeletePlanCommand.Execute(null);
            await deletionSaveAttempt.WaitForStartAsync();
            deletionSaveAttempt.CompleteWithFailure(new InvalidOperationException("Expected save failure."));
            await workspace.FlushAutosaveAsync(CancellationToken.None);
            Dispatcher.UIThread.RunJobs();

            Button retryButton = findRequiredControl<Button>(host, "EmptyWorkspaceRetryAutosaveButton");
            Assert.True(workspace.IsWorkspaceEmpty);
            Assert.True(workspace.HasAutosaveError);
            Assert.True(retryButton.IsEffectivelyVisible);
            Assert.True(retryButton.IsEnabled);
            Assert.Equal("저장 다시 시도", AutomationProperties.GetName(retryButton));

            retryButton.Command?.Execute(null);
            await retrySaveAttempt.WaitForStartAsync();
            retrySaveAttempt.CompleteSuccessfully();
            await workspace.FlushAutosaveAsync(CancellationToken.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.HasAutosaveError);
            Assert.False(retryButton.IsVisible);
            Assert.Equal(string.Empty, workspace.AutosaveStatusText);
            Assert.DoesNotContain(host.GetVisualDescendants().OfType<TextBlock>(), textBlock => textBlock.Text == "자동 저장됨");
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void FindShortcutOpensTheCoursePaneAndFocusesSearch()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(900.0));
        workspace.OpenInspectorPaneCommand.Execute(null);
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 900.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button addPlanButton = findRequiredControl<Button>(host, "AddPlanButton");
            Assert.True(addPlanButton.Focus());

            window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
            Dispatcher.UIThread.RunJobs();

            TextBox searchBox = host.GetVisualDescendants()
                .OfType<TextBox>()
                .Single(candidate => candidate.Name == "CourseSearchBox");
            Assert.True(workspace.IsCoursePaneOpen);
            Assert.True(workspace.IsInspectorPaneOpen);
            Assert.True(searchBox.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void FindShortcutDoesNotChangeTheWorkspaceBehindAModal()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(900.0));
        if (workspace.IsCoursePaneOpen)
        {
            workspace.ToggleCoursePaneCommand.Execute(null);
        }
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 900.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            workspace.BeginRenamePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            TextBox editor = findRequiredControl<TextBox>(host, "PlanNameEditor");
            Assert.True(editor.IsKeyboardFocusWithin);
            Assert.False(workspace.IsCoursePaneOpen);

            window.KeyPress(Key.F, RawInputModifiers.Control, PhysicalKey.F, "f");
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsCoursePaneOpen);
            Assert.True(editor.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

}
