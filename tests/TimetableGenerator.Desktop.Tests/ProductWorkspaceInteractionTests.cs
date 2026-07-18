using System;
using System.Linq;
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
using Avalonia.Threading;
using Avalonia.VisualTree;

using FluentIcons.Avalonia;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
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

            Assert.True(editingOverlay.IsVisible);
            Assert.False(workspaceSurface.IsEnabled);
            Assert.Equal(requestedPlanName, workspace.PlanPendingDeletionName);
            Assert.True(cancelButton.IsKeyboardFocusWithin);
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
            Point dialogPosition = findRequiredPosition(dialog, host);

            Assert.True(workspace.IsClearActivePlanConfirmationVisible);
            Assert.True(cancelButton.IsKeyboardFocusWithin);
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

            workspace.ToggleInspectorPaneCommand.Execute(null);
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
    public void LastPlanHidesItsUnavailableCloseAction()
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

            Assert.False(closeButton.IsVisible);
            Assert.False(closeButton.IsEnabled);
            Assert.False(closeButton.Command?.CanExecute(null));
            Assert.Equal(
                workspace.ActivePlan.CloseButtonAccessibleName,
                AutomationProperties.GetName(closeButton));
            Assert.Equal(
                workspace.ActivePlan.CloseButtonHelpText,
                AutomationProperties.GetHelpText(closeButton));
            Assert.Equal(
                workspace.ActivePlan.CloseButtonHelpText,
                ToolTip.GetTip(closeButton));
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
        workspace.ToggleInspectorPaneCommand.Execute(null);
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
            Assert.False(workspace.IsInspectorPaneOpen);
            Assert.True(searchBox.IsKeyboardFocusWithin);
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
            TextBlock title = findRequiredControl<TextBlock>(
                scheduleWorkspace,
                "ScheduleWorkspaceTitle");
            StackPanel headerActions = findRequiredControl<StackPanel>(
                scheduleWorkspace,
                "WorkspaceHeaderActions");
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
            assertCompoundHeaderButtonAlignment(scheduleViewMode);
            assertCompoundHeaderButtonAlignment(addPersonalSchedule);
            assertCompoundHeaderButtonAlignment(openInspector);
            assertCompoundHeaderButtonAlignment(export);
            Assert.Same(
                workspace.ToggleInspectorPaneCommand,
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
            Assert.True(
                headerActions.Children.IndexOf(openInspector)
                < headerActions.Children.IndexOf(export));

            Point titlePosition = findRequiredPosition(title, scheduleWorkspace);
            Point headerPosition = findRequiredPosition(
                headerActions,
                scheduleWorkspace);
            Point openInspectorPosition = findRequiredPosition(
                openInspector,
                scheduleWorkspace);
            Point exportPosition = findRequiredPosition(
                export,
                scheduleWorkspace);

            double titleRight = titlePosition.X + title.Bounds.Width;
            double headerRight = headerPosition.X + headerActions.Bounds.Width;
            double openInspectorRight =
                openInspectorPosition.X + openInspector.Bounds.Width;
            double openInspectorCenterY = openInspectorPosition.Y
                + (openInspector.Bounds.Height / 2.0);
            double exportCenterY = exportPosition.Y
                + (export.Bounds.Height / 2.0);

            Assert.True(titleRight <= headerPosition.X + 1.0);
            Assert.True(headerRight <= scheduleWorkspace.Bounds.Width + 1.0);
            Assert.True(openInspectorRight <= exportPosition.X + 1.0);
            Assert.InRange(
                Math.Abs(openInspectorCenterY - exportCenterY),
                0.0,
                1.0);

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
        FluentIcon icon = button.GetVisualDescendants().OfType<FluentIcon>().Single();
        TextBlock text = button.GetVisualDescendants().OfType<TextBlock>().Single();
        Point iconPosition = findRequiredPosition(icon, button);
        Point textPosition = findRequiredPosition(text, button);
        double iconCenterY = iconPosition.Y + (icon.Bounds.Height / 2.0);
        double textCenterY = textPosition.Y + (text.Bounds.Height / 2.0);

        Assert.InRange(button.Bounds.Height, 39.99, 40.01);
        Assert.InRange(Math.Abs(iconCenterY - textCenterY), 0.0, 0.5);
    }
}
