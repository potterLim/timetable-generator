using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentIcons.Avalonia;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Storage;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ProductWorkspaceInteractionTests
{
    private const double MINIMUM_PRODUCT_HEIGHT = 640.0;

    [AvaloniaFact]
    public void PlanCloseUsesACenteredModalAndPreservesTheRequestedPlanName()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1200.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            string requestedPlanName = workspace.Plans[1].DisplayName;
            workspace.Plans[1].CloseCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Grid workspaceSurface = findRequiredControl<Grid>(
                host,
                "WorkspaceSurface");
            Border editingOverlay = findRequiredControl<Border>(
                host,
                "PlanEditingOverlay");
            Border editingDialog = findRequiredControl<Border>(
                host,
                "PlanEditingDialog");
            Button cancelButton = findRequiredControl<Button>(
                host,
                "CancelDeletePlanButton");
            Button confirmButton = findRequiredControl<Button>(
                host,
                "ConfirmDeletePlanButton");
            Border iconSurface = findRequiredControl<Border>(
                host,
                "DeletePlanIconSurface");
            TextBlock heading = findRequiredControl<TextBlock>(
                host,
                "DeletePlanHeading");
            TextBlock description = findRequiredControl<TextBlock>(
                host,
                "DeletePlanDescription");
            StackPanel actions = findRequiredControl<StackPanel>(
                host,
                "DeletePlanActions");

            Assert.True(editingOverlay.IsVisible);
            Assert.False(workspaceSurface.IsEnabled);
            Assert.Equal(requestedPlanName, workspace.PlanPendingDeletionName);
            Assert.Equal(
                "삭제할 계획: '" + requestedPlanName + "'",
                workspace.PlanDeletionDescription);
            Assert.True(cancelButton.IsKeyboardFocusWithin);
            Assert.Equal(new Thickness(32.0), editingDialog.Padding);
            Assert.Equal(
                "계획 삭제 확인",
                AutomationProperties.GetName(editingDialog));
            Assert.Equal(HorizontalAlignment.Center, iconSurface.HorizontalAlignment);
            Assert.Equal("계획을 삭제할까요?", heading.Text);
            Assert.Equal(TextAlignment.Center, heading.TextAlignment);
            Assert.Equal(TextAlignment.Center, description.TextAlignment);
            Assert.Equal(HorizontalAlignment.Center, actions.HorizontalAlignment);
            Assert.Equal("계획 삭제", confirmButton.Content);
            Assert.Equal(
                HorizontalAlignment.Center,
                cancelButton.HorizontalContentAlignment);
            Assert.Equal(
                VerticalAlignment.Center,
                cancelButton.VerticalContentAlignment);
            Assert.Equal(
                HorizontalAlignment.Center,
                confirmButton.HorizontalContentAlignment);
            Assert.Equal(
                VerticalAlignment.Center,
                confirmButton.VerticalContentAlignment);
            assertCentered(editingDialog, host);

            workspace.CancelDeletePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(editingOverlay.IsVisible);
            Assert.True(workspaceSurface.IsEnabled);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void RenameEditorPlacesTheCaretAfterTheExistingPlanName()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1200.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            workspace.BeginRenamePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            TextBox editor = findRequiredControl<TextBox>(host, "PlanNameEditor");
            int expectedCaretIndex = workspace.ActivePlan.DisplayName.Length;

            Assert.True(editor.IsKeyboardFocusWithin);
            Assert.Equal(workspace.ActivePlan.DisplayName, editor.Text);
            Assert.Equal(expectedCaretIndex, editor.CaretIndex);
            Assert.Equal(expectedCaretIndex, editor.SelectionStart);
            Assert.Equal(expectedCaretIndex, editor.SelectionEnd);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void ScheduleSurfaceAndCloseActionAreTheOnlyInspectorDismissals()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(1200.0));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1200.0);

        try
        {
            window.Show();
            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            SplitView inspectorSplitView = findRequiredControl<SplitView>(
                host,
                "InspectorSplitView");
            Assert.False(inspectorSplitView.UseLightDismissOverlayMode);

            workspace.BeginRenamePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsInspectorPaneOpen);

            workspace.CancelRenamePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            ScheduleWorkspaceView scheduleWorkspace = host.GetVisualDescendants()
                .OfType<ScheduleWorkspaceView>()
                .Single();
            Grid scheduleSurface = findRequiredControl<Grid>(
                scheduleWorkspace,
                "ScheduleContentSurface");
            Point surfacePosition = findRequiredPosition(scheduleSurface, window);
            Point clickPosition = new Point(
                surfacePosition.X + 8.0,
                surfacePosition.Y + scheduleSurface.Bounds.Height - 8.0);
            window.MouseMove(clickPosition, RawInputModifiers.None);
            window.MouseDown(
                clickPosition,
                MouseButton.Left,
                RawInputModifiers.None);
            window.MouseUp(
                clickPosition,
                MouseButton.Left,
                RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsInspectorPaneOpen);

            workspace.OpenInspectorPaneCommand.Execute(null);
            workspace.CloseInspectorPaneCommand.Execute(null);

            Assert.False(workspace.IsInspectorPaneOpen);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void CompactToolbarActionOpensCoursePaneWithoutDismissingInspector()
    {
        const double COMPACT_WIDTH = 900.0;

        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(COMPACT_WIDTH));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, COMPACT_WIDTH);

        try
        {
            window.Show();
            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            ScheduleWorkspaceView scheduleWorkspace = host.GetVisualDescendants()
                .OfType<ScheduleWorkspaceView>()
                .Single();
            Button openCoursePane = findRequiredControl<Button>(
                scheduleWorkspace,
                "OpenCoursePaneButton");
            SplitView inspectorSplitView = findRequiredControl<SplitView>(
                host,
                "InspectorSplitView");
            Control lightDismissLayer = inspectorSplitView.GetVisualDescendants()
                .OfType<Control>()
                .Single(candidate => candidate.Name == "LightDismissLayer");

            Assert.Equal(
                SplitViewDisplayMode.Overlay,
                inspectorSplitView.DisplayMode);
            Assert.False(lightDismissLayer.IsHitTestVisible);

            Point buttonPosition = findRequiredPosition(openCoursePane, window);
            Point clickPosition = new Point(
                buttonPosition.X + (openCoursePane.Bounds.Width / 2.0),
                buttonPosition.Y + (openCoursePane.Bounds.Height / 2.0));

            window.MouseMove(clickPosition, RawInputModifiers.None);
            window.MouseDown(
                clickPosition,
                MouseButton.Left,
                RawInputModifiers.None);
            window.MouseUp(
                clickPosition,
                MouseButton.Left,
                RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsCoursePaneOpen);
            Assert.True(workspace.IsInspectorPaneOpen);

            workspace.ToggleCoursePaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsCoursePaneOpen);
            Assert.True(workspace.IsInspectorPaneOpen);

            Grid scheduleSurface = findRequiredControl<Grid>(
                scheduleWorkspace,
                "ScheduleContentSurface");
            Point surfacePosition = findRequiredPosition(scheduleSurface, window);
            Point scheduleClickPosition = new Point(
                surfacePosition.X + 8.0,
                surfacePosition.Y + scheduleSurface.Bounds.Height - 8.0);

            window.MouseMove(scheduleClickPosition, RawInputModifiers.None);
            window.MouseDown(
                scheduleClickPosition,
                MouseButton.Left,
                RawInputModifiers.None);
            window.MouseUp(
                scheduleClickPosition,
                MouseButton.Left,
                RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsInspectorPaneOpen);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PlanClearDialogFitsCompactWidthAndRestoresInvokerFocus()
    {
        const double COMPACT_WIDTH = 360.0;

        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.applyWorkspaceWidth(new WorkspaceWidth(COMPACT_WIDTH));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, COMPACT_WIDTH);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button invoker = findRequiredControl<Button>(host, "AddPlanButton");
            Assert.True(invoker.Focus());
            workspace.BeginClearActivePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Border dialog = findRequiredControl<Border>(host, "PlanEditingDialog");
            Button cancelButton = findRequiredControl<Button>(
                host,
                "CancelClearActivePlanButton");
            Button confirmButton = findRequiredControl<Button>(
                host,
                "ConfirmClearActivePlanButton");
            Border iconSurface = findRequiredControl<Border>(
                host,
                "ClearActivePlanIconSurface");
            TextBlock heading = findRequiredControl<TextBlock>(
                host,
                "ClearActivePlanHeading");
            TextBlock description = findRequiredControl<TextBlock>(
                host,
                "ClearActivePlanDescription");
            StackPanel actions = findRequiredControl<StackPanel>(
                host,
                "ClearActivePlanActions");
            Point dialogPosition = findRequiredPosition(dialog, host);

            Assert.True(workspace.IsClearActivePlanConfirmationVisible);
            Assert.True(cancelButton.IsKeyboardFocusWithin);
            Assert.Equal(new Thickness(32.0), dialog.Padding);
            Assert.Equal(HorizontalAlignment.Center, iconSurface.HorizontalAlignment);
            Assert.Equal("시간표를 비울까요?", heading.Text);
            Assert.Equal(TextAlignment.Center, heading.TextAlignment);
            Assert.Equal(TextAlignment.Center, description.TextAlignment);
            Assert.Equal(HorizontalAlignment.Center, actions.HorizontalAlignment);
            Assert.Equal("모두 지우기", confirmButton.Content);
            Assert.Equal(
                HorizontalAlignment.Center,
                cancelButton.HorizontalContentAlignment);
            Assert.Equal(
                VerticalAlignment.Center,
                cancelButton.VerticalContentAlignment);
            Assert.Equal(
                HorizontalAlignment.Center,
                confirmButton.HorizontalContentAlignment);
            Assert.Equal(
                VerticalAlignment.Center,
                confirmButton.VerticalContentAlignment);
            Assert.InRange(dialogPosition.X, 15.0, 17.0);
            Assert.True(
                dialogPosition.X + dialog.Bounds.Width
                <= host.Bounds.Width - 15.0);

            workspace.CancelClearActivePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsPlanEditingOverlayVisible);
            Assert.True(invoker.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PlanManagementClearActionOpensConfirmationBeforeFlyoutCloses()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1200.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            workspace.OpenInspectorPaneCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Button managementButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == "PlanManagementButton");
            Flyout? managementFlyoutOrNull = managementButton.Flyout as Flyout;
            if (managementFlyoutOrNull == null)
            {
                throw new InvalidOperationException(
                    "The plan-management action did not have a flyout.");
            }

            managementFlyoutOrNull.ShowAt(managementButton);
            Dispatcher.UIThread.RunJobs();

            Control? flyoutContentOrNull = managementFlyoutOrNull.Content as Control;
            if (flyoutContentOrNull == null)
            {
                throw new InvalidOperationException(
                    "The plan-management flyout did not have control content.");
            }

            Button clearButton = flyoutContentOrNull.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == "ClearActivePlanButton");
            TopLevel? popupTopLevelOrNull = TopLevel.GetTopLevel(clearButton);
            if (popupTopLevelOrNull == null)
            {
                throw new InvalidOperationException(
                    "The plan-management flyout was not attached to a top level.");
            }

            Point clearButtonPosition = findRequiredPosition(
                clearButton,
                popupTopLevelOrNull);
            Point clickPosition = new Point(
                clearButtonPosition.X + (clearButton.Bounds.Width / 2.0),
                clearButtonPosition.Y + (clearButton.Bounds.Height / 2.0));

            popupTopLevelOrNull.MouseMove(
                clickPosition,
                RawInputModifiers.None);
            popupTopLevelOrNull.MouseDown(
                clickPosition,
                MouseButton.Left,
                RawInputModifiers.None);
            popupTopLevelOrNull.MouseUp(
                clickPosition,
                MouseButton.Left,
                RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Button cancelButton = findRequiredControl<Button>(
                host,
                "CancelClearActivePlanButton");
            Assert.True(workspace.IsClearActivePlanConfirmationVisible);
            Assert.True(cancelButton.IsKeyboardFocusWithin);

            workspace.CancelClearActivePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PlanTabsScrollWithoutMovingTheNewPlanActionOffScreen()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        for (int planIndex = 0; planIndex < 10; ++planIndex)
        {
            workspace.AddPlanCommand.Execute(null);
            workspace.ConfirmRenamePlanCommand.Execute(null);
        }

        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 900.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ScrollViewer planTabScrollViewer = findRequiredControl<ScrollViewer>(
                host,
                "PlanTabScrollViewer");
            Button addPlanButton = findRequiredControl<Button>(host, "AddPlanButton");
            Point? addButtonPositionOrNull = addPlanButton.TranslatePoint(
                new Point(0.0, 0.0),
                host);

            Assert.True(
                planTabScrollViewer.Extent.Width
                > planTabScrollViewer.Viewport.Width);
            Assert.NotNull(addButtonPositionOrNull);
            if (addButtonPositionOrNull == null)
            {
                throw new InvalidOperationException(
                    "The new-plan action was not attached to the workspace.");
            }

            Point addButtonPosition = addButtonPositionOrNull.Value;
            Assert.True(addButtonPosition.X >= 0.0);
            Assert.True(
                addButtonPosition.X + addPlanButton.Bounds.Width
                <= host.Bounds.Width + 1.0);
            Assert.Equal(
                "새 계획 추가",
                AutomationProperties.GetName(addPlanButton));
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void PlanTabsExposeTheirNamesAndSelectionWithoutDuplicateText()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 900.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            TabStrip planTabs = host.GetVisualDescendants()
                .OfType<TabStrip>()
                .Single(
                    candidate => AutomationProperties.GetName(candidate)
                        == "계획 목록");
            TabStripItem[] planTabItems = planTabs.GetVisualDescendants()
                .OfType<TabStripItem>()
                .ToArray();

            Assert.Equal(workspace.Plans.Count, planTabItems.Length);
            foreach (PlanTabItem plan in workspace.Plans)
            {
                TabStripItem planTab = planTabItems.Single(
                    candidate => ReferenceEquals(candidate.DataContext, plan));
                TextBlock displayText = planTab.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(candidate => candidate.Text == plan.DisplayName);
                AutomationPeer peer =
                    ControlAutomationPeer.CreatePeerForElement(planTab);
                ISelectionItemProvider selectionProvider =
                    Assert.IsAssignableFrom<ISelectionItemProvider>(peer);

                Assert.Equal(
                    plan.DisplayName,
                    AutomationProperties.GetName(planTab));
                Assert.Equal(plan.DisplayName, peer.GetName());
                Assert.Equal(
                    AutomationControlType.ListItem,
                    peer.GetAutomationControlType());
                Assert.Equal(
                    ReferenceEquals(plan, workspace.ActivePlan),
                    selectionProvider.IsSelected);
                Assert.Equal(
                    AccessibilityView.Raw,
                    AutomationProperties.GetAccessibilityView(displayText));

                StackPanel contextMenuOwner = planTab.GetVisualDescendants()
                    .OfType<StackPanel>()
                    .Single(
                        candidate => candidate.ContextMenu != null);
                ContextMenu? contextMenuOrNull = contextMenuOwner.ContextMenu;
                Assert.NotNull(contextMenuOrNull);
                if (contextMenuOrNull == null)
                {
                    throw new InvalidOperationException(
                        "The plan tab did not expose its context menu.");
                }

                MenuItem[] contextMenuItems = contextMenuOrNull.Items
                    .OfType<MenuItem>()
                    .ToArray();
                Assert.Equal(2, contextMenuItems.Length);
                MenuItem renameMenuItem = contextMenuItems[0];
                MenuItem deleteMenuItem = contextMenuItems[1];
                Assert.Equal("이름 바꾸기", renameMenuItem.Header);
                Assert.Same(plan.RenameCommand, renameMenuItem.Command);
                Assert.Equal("계획 삭제", deleteMenuItem.Header);
                Assert.Same(plan.CloseCommand, deleteMenuItem.Command);
                Assert.Contains("destructive", deleteMenuItem.Classes);

                deleteMenuItem.Command?.Execute(null);
                Assert.Equal(plan.DisplayName, workspace.PlanPendingDeletionName);
                workspace.CancelDeletePlanCommand.Execute(null);

                Button closeButton = planTab.GetVisualDescendants()
                    .OfType<Button>()
                    .Single();
                Assert.Equal(
                    plan.CloseButtonAccessibleName,
                    AutomationProperties.GetName(closeButton));
                Assert.Equal(
                    plan.CloseButtonHelpText,
                    AutomationProperties.GetHelpText(closeButton));
            }
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void LastPlanDeletionShowsTheEmptyWorkspaceAndCreationAction()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        workspace.Plans[1].CloseCommand.Execute(null);
        workspace.ConfirmDeletePlanCommand.Execute(null);

        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 900.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Button closeButton = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => ReferenceEquals(
                        candidate.Command,
                        workspace.ActivePlan.CloseCommand));

            Assert.True(closeButton.IsVisible);
            Assert.True(closeButton.IsEnabled);
            Assert.True(closeButton.Command?.CanExecute(null));
            Assert.Equal(
                workspace.ActivePlan.CloseButtonAccessibleName,
                AutomationProperties.GetName(closeButton));
            Assert.Equal(
                workspace.ActivePlan.CloseButtonHelpText,
                AutomationProperties.GetHelpText(closeButton));
            Assert.Equal(
                workspace.ActivePlan.CloseButtonHelpText,
                ToolTip.GetTip(closeButton));

            closeButton.Command?.Execute(null);
            workspace.ConfirmDeletePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Border emptyWorkspaceState = findRequiredControl<Border>(
                host,
                "EmptyWorkspaceState");
            Border planNavigationBar = findRequiredControl<Border>(
                host,
                "PlanNavigationBar");
            SplitView planningWorkspaceContent = findRequiredControl<SplitView>(
                host,
                "PlanningWorkspaceContent");
            Button createFirstPlanButton = findRequiredControl<Button>(
                host,
                "CreateFirstPlanButton");

            Assert.Empty(workspace.Plans);
            Assert.Null(workspace.ActivePlanOrNull);
            Assert.False(workspace.HasActivePlan);
            Assert.True(workspace.IsWorkspaceEmpty);
            Assert.True(emptyWorkspaceState.IsEffectivelyVisible);
            Assert.False(planNavigationBar.IsVisible);
            Assert.False(planningWorkspaceContent.IsVisible);
            Assert.True(createFirstPlanButton.IsEffectivelyVisible);
            Assert.True(createFirstPlanButton.IsKeyboardFocusWithin);
            Assert.Equal(
                "새 시간표 만들기",
                AutomationProperties.GetName(createFirstPlanButton));

            createFirstPlanButton.Command?.Execute(null);

            PlanTabItem createdPlan = Assert.Single(workspace.Plans);
            Assert.Same(createdPlan, workspace.ActivePlan);
            Assert.Equal("2026-2학기 시간표", createdPlan.DisplayName);
            Assert.True(workspace.HasActivePlan);
            Assert.False(workspace.IsWorkspaceEmpty);
            Assert.True(planNavigationBar.IsVisible);
            Assert.True(workspace.IsRenamingPlan);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public void WorkspaceWithoutPlansStartsInTheEmptyExperience()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspaceWithoutPlans();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_280.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Border emptyWorkspaceState = findRequiredControl<Border>(
                host,
                "EmptyWorkspaceState");
            Border planNavigationBar = findRequiredControl<Border>(
                host,
                "PlanNavigationBar");
            SplitView planningWorkspaceContent = findRequiredControl<SplitView>(
                host,
                "PlanningWorkspaceContent");
            Button createFirstPlanButton = findRequiredControl<Button>(
                host,
                "CreateFirstPlanButton");

            Assert.Empty(workspace.Plans);
            Assert.Null(workspace.ActivePlanOrNull);
            Assert.True(workspace.IsWorkspaceEmpty);
            Assert.True(emptyWorkspaceState.IsEffectivelyVisible);
            Assert.False(planNavigationBar.IsVisible);
            Assert.False(planningWorkspaceContent.IsVisible);
            Assert.True(createFirstPlanButton.IsEffectivelyVisible);

            createFirstPlanButton.Command?.Execute(null);

            PlanTabItem createdPlan = Assert.Single(workspace.Plans);
            Assert.Equal("2026-2학기 시간표", createdPlan.DisplayName);
            Assert.Same(createdPlan, workspace.ActivePlan);
            Assert.True(planNavigationBar.IsVisible);
            Assert.True(workspace.IsRenamingPlan);
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
        ControlledPlanningWorkspaceStore store =
            new ControlledPlanningWorkspaceStore();
        ControlledSaveAttempt creationSaveAttempt = new ControlledSaveAttempt();
        ControlledSaveAttempt deletionSaveAttempt = new ControlledSaveAttempt();
        ControlledSaveAttempt retrySaveAttempt = new ControlledSaveAttempt();
        store.EnqueueSaveAttempt(creationSaveAttempt);
        store.EnqueueSaveAttempt(deletionSaveAttempt);
        store.EnqueueSaveAttempt(retrySaveAttempt);
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspaceWithoutPlans(store);
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_280.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Button createFirstPlanButton = findRequiredControl<Button>(
                host,
                "CreateFirstPlanButton");
            createFirstPlanButton.Command?.Execute(null);
            await creationSaveAttempt.WaitForStartAsync();
            creationSaveAttempt.CompleteSuccessfully();
            await workspace.FlushAutosaveAsync(CancellationToken.None);
            workspace.CancelRenamePlanCommand.Execute(null);

            workspace.ActivePlan.CloseCommand.Execute(null);
            workspace.ConfirmDeletePlanCommand.Execute(null);
            await deletionSaveAttempt.WaitForStartAsync();
            deletionSaveAttempt.CompleteWithFailure(
                new InvalidOperationException("Expected save failure."));
            await workspace.FlushAutosaveAsync(CancellationToken.None);
            Dispatcher.UIThread.RunJobs();

            Button retryButton = findRequiredControl<Button>(
                host,
                "EmptyWorkspaceRetryAutosaveButton");
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
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
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

            window.KeyPress(
                Key.F,
                RawInputModifiers.Control,
                PhysicalKey.F,
                "f");
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
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
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

            window.KeyPress(
                Key.F,
                RawInputModifiers.Control,
                PhysicalKey.F,
                "f");
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

    [AvaloniaFact]
    public void InvalidPlanNameReturnsFocusAndAnnouncesTheValidationMessage()
    {
        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1200.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            workspace.BeginRenamePlanCommand.Execute(null);
            workspace.PlanNameDraft = workspace.Plans
                .Single(plan => plan.PlanId != workspace.ActivePlan.PlanId)
                .DisplayName;
            workspace.ConfirmRenamePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            TextBox editor = findRequiredControl<TextBox>(host, "PlanNameEditor");
            TextBlock validationMessage = findRequiredControl<TextBlock>(
                host,
                "PlanNameValidationMessage");

            Assert.True(workspace.HasPlanNameValidationMessage);
            Assert.True(editor.IsKeyboardFocusWithin);
            Assert.Equal(0, editor.SelectionStart);
            Assert.Equal(editor.Text?.Length ?? 0, editor.SelectionEnd);
            Assert.Equal(
                workspace.PlanNameValidationMessage,
                AutomationProperties.GetHelpText(editor));
            Assert.Equal(
                AutomationLiveSetting.Assertive,
                AutomationProperties.GetLiveSetting(validationMessage));
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    [AvaloniaFact]
    public async Task HeaderActionsFitAndOpenInspectorAtMediumBreakpointAsync()
    {
        const double MEDIUM_BREAKPOINT_WIDTH = 1_080.0;

        PlannerWorkspaceViewModel workspace =
            PlannerWorkspaceTestFactory.CreateWorkspace();
        await workspace.RecommendationRefreshTask;
        workspace.applyWorkspaceWidth(new WorkspaceWidth(
            MEDIUM_BREAKPOINT_WIDTH));
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, MEDIUM_BREAKPOINT_WIDTH);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ScheduleWorkspaceView scheduleWorkspace = host.GetVisualDescendants()
                .OfType<ScheduleWorkspaceView>()
                .Single();
            Grid commandBar = findRequiredControl<Grid>(
                scheduleWorkspace,
                "WorkspaceCommandBar");
            StackPanel supportingActions = findRequiredControl<StackPanel>(
                scheduleWorkspace,
                "WorkspaceSupportingActions");
            StackPanel headerActions = findRequiredControl<StackPanel>(
                scheduleWorkspace,
                "WorkspaceHeaderActions");
            Button openCoursePane = findRequiredControl<Button>(
                scheduleWorkspace,
                "OpenCoursePaneButton");
            Button scheduleViewMode = findRequiredControl<Button>(
                scheduleWorkspace,
                "ScheduleViewModeButton");
            Button addPersonalSchedule = findRequiredControl<Button>(
                scheduleWorkspace,
                "WorkspaceAddPersonalScheduleButton");
            Button openInspector = findRequiredControl<Button>(
                scheduleWorkspace,
                "OpenInspectorPaneButton");
            Button export = findRequiredControl<Button>(
                scheduleWorkspace,
                "ExportScheduleButton");

            Assert.True(openInspector.IsEffectivelyVisible);
            Assert.True(export.IsEffectivelyVisible);
            Assert.False(openCoursePane.IsEffectivelyVisible);
            Assert.Equal(
                "시간표 작업 영역",
                AutomationProperties.GetName(scheduleWorkspace));
            Assert.Null(scheduleWorkspace.FindControl<TextBlock>(
                "ScheduleWorkspaceTitle"));
            Assert.Equal(40.0, commandBar.Bounds.Height);
            Assert.True(
                supportingActions.Children.IndexOf(openCoursePane)
                < supportingActions.Children.IndexOf(scheduleViewMode));
            assertCompoundHeaderButtonAlignment(scheduleViewMode);
            assertCompoundHeaderButtonAlignment(addPersonalSchedule);
            assertCompoundHeaderButtonAlignment(openInspector);
            assertCompoundHeaderButtonAlignment(export);
            Assert.Same(
                workspace.OpenInspectorPaneCommand,
                openInspector.Command);
            Assert.Equal(
                "내 계획 패널 열기",
                AutomationProperties.GetName(openInspector));
            Assert.Equal(
                "OpenInspectorPane",
                AutomationProperties.GetAutomationId(openInspector));
            Assert.Equal("내 계획 열기", ToolTip.GetTip(openInspector));
            Assert.Contains(
                openInspector.GetVisualDescendants().OfType<TextBlock>(),
                candidate => candidate.Text == "내 계획 열기");
            Assert.True(openInspector.Focusable);
            Assert.True(openInspector.IsTabStop);
            Assert.DoesNotContain(
                scheduleWorkspace.GetVisualDescendants().OfType<TextBlock>(),
                candidate => candidate.Text == "추천 시간표");
            Assert.True(
                headerActions.Children.IndexOf(openInspector)
                < headerActions.Children.IndexOf(export));

            Point commandBarPosition = findRequiredPosition(
                commandBar,
                scheduleWorkspace);
            Point supportingPosition = findRequiredPosition(
                supportingActions,
                scheduleWorkspace);
            Point headerPosition = findRequiredPosition(
                headerActions,
                scheduleWorkspace);
            Point scheduleViewModePosition = findRequiredPosition(
                scheduleViewMode,
                scheduleWorkspace);
            Point addPersonalSchedulePosition = findRequiredPosition(
                addPersonalSchedule,
                scheduleWorkspace);
            Point openInspectorPosition = findRequiredPosition(
                openInspector,
                scheduleWorkspace);
            Point exportPosition = findRequiredPosition(
                export,
                scheduleWorkspace);

            double supportingRight =
                supportingPosition.X + supportingActions.Bounds.Width;
            double headerRight = headerPosition.X + headerActions.Bounds.Width;
            double scheduleViewModeCenterY = scheduleViewModePosition.Y
                + (scheduleViewMode.Bounds.Height / 2.0);
            double addPersonalScheduleCenterY = addPersonalSchedulePosition.Y
                + (addPersonalSchedule.Bounds.Height / 2.0);
            double openInspectorRight =
                openInspectorPosition.X + openInspector.Bounds.Width;
            double openInspectorCenterY = openInspectorPosition.Y
                + (openInspector.Bounds.Height / 2.0);
            double exportCenterY = exportPosition.Y
                + (export.Bounds.Height / 2.0);

            Assert.Equal(18.0, commandBarPosition.Y);
            Assert.True(supportingRight <= headerPosition.X + 1.0);
            Assert.True(headerRight <= scheduleWorkspace.Bounds.Width + 1.0);
            Assert.True(openInspectorRight <= exportPosition.X + 1.0);
            Assert.InRange(
                Math.Abs(scheduleViewModeCenterY - addPersonalScheduleCenterY),
                0.0,
                1.0);
            Assert.InRange(
                Math.Abs(openInspectorCenterY - exportCenterY),
                0.0,
                1.0);

            workspace.IsCoursePaneOpen = false;
            Dispatcher.UIThread.RunJobs();

            Assert.True(openCoursePane.IsEffectivelyVisible);
            Point openCoursePanePosition = findRequiredPosition(
                openCoursePane,
                scheduleWorkspace);
            scheduleViewModePosition = findRequiredPosition(
                scheduleViewMode,
                scheduleWorkspace);
            Assert.True(
                openCoursePanePosition.X + openCoursePane.Bounds.Width
                <= scheduleViewModePosition.X + 1.0);

            workspace.IsCoursePaneOpen = true;
            Dispatcher.UIThread.RunJobs();

            Assert.False(openCoursePane.IsEffectivelyVisible);

            openInspector.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.IsInspectorPaneOpen);
            Button closeInspector = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == "CloseInspectorPaneButton");
            Assert.True(closeInspector.IsEffectivelyVisible);
            Assert.True(closeInspector.IsKeyboardFocusWithin);

            closeInspector.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.IsInspectorPaneOpen);
            Assert.True(openInspector.IsKeyboardFocusWithin);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

    private static Window createWindow(Control content, double width)
    {
        Window window = new Window();
        window.Width = width;
        window.Height = MINIMUM_PRODUCT_HEIGHT;
        window.Content = content;
        return window;
    }

    private static TControl findRequiredControl<TControl>(
        Control root,
        string controlName)
        where TControl : Control
    {
        TControl? controlOrNull = root.FindControl<TControl>(controlName);
        if (controlOrNull == null)
        {
            throw new InvalidOperationException(
                "The required workspace control was not found: " + controlName);
        }

        return controlOrNull;
    }

    private static Point findRequiredPosition(Control control, Control relativeTo)
    {
        Point? positionOrNull = control.TranslatePoint(
            new Point(0.0, 0.0),
            relativeTo);
        if (positionOrNull == null)
        {
            throw new InvalidOperationException(
                "The workspace control was not attached to the requested surface.");
        }

        return positionOrNull.Value;
    }

    private static void assertCentered(Control dialog, Control host)
    {
        Point? dialogPositionOrNull = dialog.TranslatePoint(
            new Point(0.0, 0.0),
            host);
        Assert.NotNull(dialogPositionOrNull);
        if (dialogPositionOrNull == null)
        {
            throw new InvalidOperationException(
                "The plan dialog was not attached to the workspace.");
        }

        Point dialogPosition = dialogPositionOrNull.Value;
        double dialogCenterX = dialogPosition.X + (dialog.Bounds.Width / 2.0);
        double dialogCenterY = dialogPosition.Y + (dialog.Bounds.Height / 2.0);
        Assert.InRange(Math.Abs(dialogCenterX - (host.Bounds.Width / 2.0)), 0.0, 1.0);
        Assert.InRange(Math.Abs(dialogCenterY - (host.Bounds.Height / 2.0)), 0.0, 1.0);
    }

    private static void assertCompoundHeaderButtonAlignment(Button button)
    {
        FluentIcon[] icons = button
            .GetVisualDescendants()
            .OfType<FluentIcon>()
            .ToArray();
        TextBlock text = button.GetVisualDescendants().OfType<TextBlock>().Single();
        Point textPosition = findRequiredPosition(text, button);
        double textCenterY = textPosition.Y + (text.Bounds.Height / 2.0);

        Assert.NotEmpty(icons);
        Assert.InRange(button.Bounds.Height, 39.99, 40.01);
        foreach (FluentIcon icon in icons)
        {
            Point iconPosition = findRequiredPosition(icon, button);
            double iconCenterY = iconPosition.Y + (icon.Bounds.Height / 2.0);
            Assert.InRange(Math.Abs(iconCenterY - textCenterY), 0.0, 0.5);
        }
    }
}
