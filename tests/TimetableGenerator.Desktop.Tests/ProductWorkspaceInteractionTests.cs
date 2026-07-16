using System;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

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
        workspace.IsCoursePaneOpen = false;
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
            Assert.True(searchBox.IsKeyboardFocusWithin);
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
}
