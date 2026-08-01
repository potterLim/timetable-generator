using System;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ProductWorkspaceInteractionTests
{
    [AvaloniaFact]
    public void PlanTabsScrollWithoutMovingTheNewPlanActionOffScreen()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        for (int planIndex = 0; planIndex < 10; ++planIndex)
        {
            workspace.AddPlanCommand.Execute(null);
            workspace.ConfirmPlanNameCommand.Execute(null);
        }

        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 900.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            ScrollViewer planTabScrollViewer = findRequiredControl<ScrollViewer>(host, "PlanTabScrollViewer");
            Button addPlanButton = findRequiredControl<Button>(host, "AddPlanButton");
            Point? addButtonPositionOrNull = addPlanButton.TranslatePoint(new Point(0.0, 0.0), host);

            Assert.True(planTabScrollViewer.Extent.Width > planTabScrollViewer.Viewport.Width);
            Assert.NotNull(addButtonPositionOrNull);
            if (addButtonPositionOrNull == null)
            {
                throw new InvalidOperationException("The new-plan action was not attached to the workspace.");
            }

            Point addButtonPosition = addButtonPositionOrNull.Value;
            Assert.True(addButtonPosition.X >= 0.0);
            Assert.True(addButtonPosition.X + addPlanButton.Bounds.Width <= host.Bounds.Width + 1.0);
            Assert.Equal("새 시간표 만들기", AutomationProperties.GetName(addPlanButton));
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
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
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
                        == "시간표 탭 목록");
            TabStripItem[] planTabItems = planTabs.GetVisualDescendants().OfType<TabStripItem>().ToArray();

            Assert.Equal(workspace.Plans.Count, planTabItems.Length);
            foreach (PlanTabItem plan in workspace.Plans)
            {
                TabStripItem planTab = planTabItems.Single(
                    candidate => ReferenceEquals(candidate.DataContext, plan));
                TextBlock displayText = planTab.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(candidate => candidate.Text == plan.DisplayName);
                AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(planTab);
                ISelectionItemProvider selectionProvider = Assert.IsAssignableFrom<ISelectionItemProvider>(peer);

                Assert.Equal(plan.DisplayName, AutomationProperties.GetName(planTab));
                Assert.Equal(plan.DisplayName, peer.GetName());
                Assert.Equal(AutomationControlType.ListItem, peer.GetAutomationControlType());
                Assert.Equal(ReferenceEquals(plan, workspace.ActivePlan), selectionProvider.IsSelected);
                Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(displayText));

                StackPanel contextMenuOwner = planTab.GetVisualDescendants()
                    .OfType<StackPanel>()
                    .Single(
                        candidate => candidate.ContextMenu != null);
                ContextMenu? contextMenuOrNull = contextMenuOwner.ContextMenu;
                Assert.NotNull(contextMenuOrNull);
                if (contextMenuOrNull == null)
                {
                    throw new InvalidOperationException("The plan tab did not expose its context menu.");
                }

                MenuItem[] contextMenuItems = contextMenuOrNull.Items.OfType<MenuItem>().ToArray();
                Assert.Equal(2, contextMenuItems.Length);
                MenuItem renameMenuItem = contextMenuItems[0];
                MenuItem deleteMenuItem = contextMenuItems[1];
                Assert.Equal("이름 바꾸기", renameMenuItem.Header);
                Assert.Same(plan.RenameCommand, renameMenuItem.Command);
                Assert.Equal("시간표 삭제", deleteMenuItem.Header);
                Assert.Same(plan.CloseCommand, deleteMenuItem.Command);
                Assert.Contains("destructive", deleteMenuItem.Classes);

                deleteMenuItem.Command?.Execute(null);
                Assert.Equal(plan.DisplayName, workspace.PlanPendingDeletionName);
                workspace.CancelDeletePlanCommand.Execute(null);

                Button closeButton = planTab.GetVisualDescendants().OfType<Button>().Single();
                Assert.Equal(plan.CloseButtonAccessibleName, AutomationProperties.GetName(closeButton));
                Assert.Equal(plan.CloseButtonHelpText, AutomationProperties.GetHelpText(closeButton));
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
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
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
            Assert.Equal(workspace.ActivePlan.CloseButtonAccessibleName, AutomationProperties.GetName(closeButton));
            Assert.Equal(workspace.ActivePlan.CloseButtonHelpText, AutomationProperties.GetHelpText(closeButton));
            Assert.Equal(workspace.ActivePlan.CloseButtonHelpText, ToolTip.GetTip(closeButton));

            closeButton.Command?.Execute(null);
            workspace.ConfirmDeletePlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Border emptyWorkspaceState = findRequiredControl<Border>(host, "EmptyWorkspaceState");
            Border planNavigationBar = findRequiredControl<Border>(host, "PlanNavigationBar");
            Grid planningWorkspaceContent = findRequiredControl<Grid>(host, "PlanningWorkspaceContent");
            Button createFirstPlanButton = findRequiredControl<Button>(host, "CreateFirstPlanButton");

            Assert.Empty(workspace.Plans);
            Assert.Null(workspace.ActivePlanOrNull);
            Assert.False(workspace.HasActivePlan);
            Assert.True(workspace.IsWorkspaceEmpty);
            Assert.True(emptyWorkspaceState.IsEffectivelyVisible);
            Assert.False(planNavigationBar.IsVisible);
            Assert.False(planningWorkspaceContent.IsVisible);
            Assert.True(createFirstPlanButton.IsEffectivelyVisible);
            Assert.True(createFirstPlanButton.IsKeyboardFocusWithin);
            Assert.Equal("새 시간표 만들기", AutomationProperties.GetName(createFirstPlanButton));

            createFirstPlanButton.Command?.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(workspace.Plans);
            Assert.True(workspace.IsCreatingPlan);
            Assert.False(workspace.IsRenamingPlan);
            Assert.Equal("시간표 이름", workspace.PlanNameEditorTitle);
            Assert.Equal("만들기", workspace.PlanNameEditorPrimaryActionText);
            Assert.Equal("시간표 이름", workspace.PlanEditingDialogAccessibleName);
            Border planEditingDialog = findRequiredControl<Border>(host, "PlanEditingDialog");
            Assert.Equal("시간표 이름", AutomationProperties.GetName(planEditingDialog));
            TextBox planNameEditor = findRequiredControl<TextBox>(host, "PlanNameEditor");
            int expectedCaretIndex = workspace.PlanNameDraft.Length;
            Assert.True(planNameEditor.IsKeyboardFocusWithin);
            Assert.Equal(expectedCaretIndex, planNameEditor.CaretIndex);
            Assert.Equal(expectedCaretIndex, planNameEditor.SelectionStart);
            Assert.Equal(expectedCaretIndex, planNameEditor.SelectionEnd);

            workspace.ConfirmPlanNameCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            PlanTabItem createdPlan = Assert.Single(workspace.Plans);
            Assert.Same(createdPlan, workspace.ActivePlan);
            Assert.Equal("2026-2학기 시간표", createdPlan.DisplayName);
            Assert.True(workspace.HasActivePlan);
            Assert.False(workspace.IsWorkspaceEmpty);
            Assert.True(planNavigationBar.IsVisible);
            Assert.False(workspace.IsPlanNameEditorVisible);
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
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspaceWithoutPlans();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1_280.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            Border emptyWorkspaceState = findRequiredControl<Border>(host, "EmptyWorkspaceState");
            Border planNavigationBar = findRequiredControl<Border>(host, "PlanNavigationBar");
            Grid planningWorkspaceContent = findRequiredControl<Grid>(host, "PlanningWorkspaceContent");
            Button createFirstPlanButton = findRequiredControl<Button>(host, "CreateFirstPlanButton");

            Assert.Empty(workspace.Plans);
            Assert.Null(workspace.ActivePlanOrNull);
            Assert.True(workspace.IsWorkspaceEmpty);
            Assert.True(emptyWorkspaceState.IsEffectivelyVisible);
            Assert.False(planNavigationBar.IsVisible);
            Assert.False(planningWorkspaceContent.IsVisible);
            Assert.True(createFirstPlanButton.IsEffectivelyVisible);

            createFirstPlanButton.Command?.Execute(null);

            Assert.Empty(workspace.Plans);
            Assert.Null(workspace.ActivePlanOrNull);
            Assert.True(workspace.IsCreatingPlan);
            Assert.Equal("2026-2학기 시간표", workspace.PlanNameDraft);

            workspace.CancelPlanNameCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(workspace.Plans);
            Assert.Null(workspace.ActivePlanOrNull);
            Assert.True(workspace.IsWorkspaceEmpty);
            Assert.False(workspace.IsPlanNameEditorVisible);
            Assert.True(emptyWorkspaceState.IsEffectivelyVisible);
            Assert.False(planNavigationBar.IsVisible);
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

}
