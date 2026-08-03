using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Layout;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed partial class ProductWorkspaceInteractionTests
{
    [AvaloniaFact]
    public void PlanCloseUsesACenteredModalAndPreservesTheRequestedPlanName()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
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

            Grid workspaceSurface = findRequiredControl<Grid>(host, "WorkspaceSurface");
            Border editingOverlay = findRequiredControl<Border>(host, "PlanEditingOverlay");
            Border editingDialog = findRequiredControl<Border>(host, "PlanEditingDialog");
            Button cancelButton = findRequiredControl<Button>(host, "CancelDeletePlanButton");
            Button confirmButton = findRequiredControl<Button>(host, "ConfirmDeletePlanButton");
            Border iconSurface = findRequiredControl<Border>(host, "DeletePlanIconSurface");
            TextBlock heading = findRequiredControl<TextBlock>(host, "DeletePlanHeading");
            TextBlock description = findRequiredControl<TextBlock>(host, "DeletePlanDescription");
            StackPanel actions = findRequiredControl<StackPanel>(host, "DeletePlanActions");

            Assert.True(editingOverlay.IsVisible);
            Assert.False(workspaceSurface.IsEnabled);
            Assert.Equal(requestedPlanName, workspace.PlanPendingDeletionName);
            Assert.Equal("삭제할 시간표: '" + requestedPlanName + "'", workspace.PlanDeletionDescription);
            Assert.True(cancelButton.IsKeyboardFocusWithin);
            Assert.Equal(384.0, editingDialog.MaxWidth);
            Assert.Equal(384.0, editingDialog.Bounds.Width);
            Assert.Equal(new Thickness(24.0), editingDialog.Padding);
            Assert.Equal("시간표 삭제 확인", AutomationProperties.GetName(editingDialog));
            Assert.Equal(HorizontalAlignment.Center, iconSurface.HorizontalAlignment);
            Assert.Equal("시간표를 삭제할까요?", heading.Text);
            Assert.Equal(TextAlignment.Center, heading.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, heading.TextWrapping);
            Assert.Equal(TextAlignment.Center, description.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
            Assert.Equal(HorizontalAlignment.Center, actions.HorizontalAlignment);
            Assert.Equal("시간표 삭제", confirmButton.Content);
            Assert.Equal(HorizontalAlignment.Center, cancelButton.HorizontalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, cancelButton.VerticalContentAlignment);
            Assert.Equal(HorizontalAlignment.Center, confirmButton.HorizontalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, confirmButton.VerticalContentAlignment);
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
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
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

            Assert.True(workspace.IsRenamingPlan);
            Assert.False(workspace.IsCreatingPlan);
            Assert.Equal("시간표 이름 바꾸기", workspace.PlanNameEditorTitle);
            Assert.Equal("저장", workspace.PlanNameEditorPrimaryActionText);
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
    public void PlanCreationEditorUsesCreationCopyWithoutCreatingAPrematurePlan()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        int originalPlanCount = workspace.Plans.Count;
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1200.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            workspace.AddPlanCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Border dialog = findRequiredControl<Border>(host, "PlanEditingDialog");
            TextBox editor = findRequiredControl<TextBox>(host, "PlanNameEditor");
            Button primaryAction = host.GetVisualDescendants()
                .OfType<Button>()
                .Single(
                    candidate => ReferenceEquals(
                        candidate.Command,
                        workspace.ConfirmPlanNameCommand));
            int expectedCaretIndex = workspace.PlanNameDraft.Length;

            Assert.Equal(originalPlanCount, workspace.Plans.Count);
            Assert.True(workspace.IsCreatingPlan);
            Assert.False(workspace.IsRenamingPlan);
            Assert.Equal("시간표 이름", workspace.PlanNameEditorTitle);
            Assert.Equal("만들기", workspace.PlanNameEditorPrimaryActionText);
            Assert.Equal("시간표 이름", AutomationProperties.GetName(dialog));
            Assert.Equal("만들기", primaryAction.Content);
            Assert.True(editor.IsKeyboardFocusWithin);
            Assert.Equal(expectedCaretIndex, editor.CaretIndex);
            Assert.Equal(expectedCaretIndex, editor.SelectionStart);
            Assert.Equal(expectedCaretIndex, editor.SelectionEnd);

            workspace.CancelPlanNameCommand.Execute(null);

            Assert.Equal(originalPlanCount, workspace.Plans.Count);
            Assert.False(workspace.IsPlanNameEditorVisible);
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

        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
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
            Button cancelButton = findRequiredControl<Button>(host, "CancelClearActivePlanButton");
            Button confirmButton = findRequiredControl<Button>(host, "ConfirmClearActivePlanButton");
            Border iconSurface = findRequiredControl<Border>(host, "ClearActivePlanIconSurface");
            TextBlock heading = findRequiredControl<TextBlock>(host, "ClearActivePlanHeading");
            TextBlock description = findRequiredControl<TextBlock>(host, "ClearActivePlanDescription");
            StackPanel actions = findRequiredControl<StackPanel>(host, "ClearActivePlanActions");
            Point dialogPosition = findRequiredPosition(dialog, host);

            Assert.True(workspace.IsClearActivePlanConfirmationVisible);
            Assert.True(cancelButton.IsKeyboardFocusWithin);
            Assert.Equal(384.0, dialog.MaxWidth);
            Assert.Equal(new Thickness(24.0), dialog.Padding);
            Assert.Equal(HorizontalAlignment.Center, iconSurface.HorizontalAlignment);
            Assert.Equal("시간표를 비울까요?", heading.Text);
            Assert.Equal(TextAlignment.Center, heading.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, heading.TextWrapping);
            Assert.Equal(TextAlignment.Center, description.TextAlignment);
            Assert.Equal(TextWrapping.Wrap, description.TextWrapping);
            Assert.Equal(HorizontalAlignment.Center, actions.HorizontalAlignment);
            Assert.Equal("모두 지우기", confirmButton.Content);
            Assert.Equal(HorizontalAlignment.Center, cancelButton.HorizontalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, cancelButton.VerticalContentAlignment);
            Assert.Equal(HorizontalAlignment.Center, confirmButton.HorizontalContentAlignment);
            Assert.Equal(VerticalAlignment.Center, confirmButton.VerticalContentAlignment);
            Assert.InRange(dialogPosition.X, 15.0, 17.0);
            Assert.True(dialogPosition.X + dialog.Bounds.Width <= host.Bounds.Width - 15.0);

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
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
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
                throw new InvalidOperationException("The plan-management action did not have a flyout.");
            }

            Point managementButtonPosition = findRequiredPosition(managementButton, window);
            Point managementClickPosition = new Point(managementButtonPosition.X + (managementButton.Bounds.Width / 2.0), managementButtonPosition.Y + (managementButton.Bounds.Height / 2.0));
            window.MouseMove(managementClickPosition, RawInputModifiers.None);
            window.MouseDown(managementClickPosition, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(managementClickPosition, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(managementFlyoutOrNull.IsOpen);

            Control? flyoutContentOrNull = managementFlyoutOrNull.Content as Control;
            if (flyoutContentOrNull == null)
            {
                throw new InvalidOperationException("The plan-management flyout did not have control content.");
            }

            Button clearButton = flyoutContentOrNull.GetVisualDescendants()
                .OfType<Button>()
                .Single(candidate => candidate.Name == "ClearActivePlanButton");
            TopLevel? popupTopLevelOrNull = TopLevel.GetTopLevel(clearButton);
            if (popupTopLevelOrNull == null)
            {
                throw new InvalidOperationException("The plan-management flyout was not attached to a top level.");
            }

            Point clearButtonPosition = findRequiredPosition(clearButton, popupTopLevelOrNull);
            Point clickPosition = new Point(clearButtonPosition.X + (clearButton.Bounds.Width / 2.0), clearButtonPosition.Y + (clearButton.Bounds.Height / 2.0));

            popupTopLevelOrNull.MouseMove(clickPosition, RawInputModifiers.None);
            popupTopLevelOrNull.MouseDown(clickPosition, MouseButton.Left, RawInputModifiers.None);
            popupTopLevelOrNull.MouseUp(clickPosition, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Button cancelButton = findRequiredControl<Button>(host, "CancelClearActivePlanButton");
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
    public void InvalidPlanNameReturnsFocusAndAnnouncesTheValidationMessage()
    {
        PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace();
        ProductWorkspaceHostView host = new ProductWorkspaceHostView();
        host.DataContext = workspace;
        Window window = createWindow(host, 1200.0);

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();

            TextBlock validationMessage = findRequiredControl<TextBlock>(host, "PlanNameValidationMessage");
            Assert.True(string.IsNullOrEmpty(validationMessage.Text));
            Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(validationMessage));
            List<(string? Text, AutomationLiveSetting LiveSetting)> liveRegionTransitions = new List<(string? Text, AutomationLiveSetting LiveSetting)>();
            validationMessage.PropertyChanged += (object? senderOrNull, AvaloniaPropertyChangedEventArgs eventArgs) =>
            {
                if (eventArgs.Property == TextBlock.TextProperty)
                {
                    liveRegionTransitions.Add((validationMessage.Text, AutomationProperties.GetLiveSetting(validationMessage)));
                }
            };

            workspace.BeginRenamePlanCommand.Execute(null);
            string duplicatePlanName = workspace.Plans
                .Single(plan => plan.PlanId != workspace.ActivePlan.PlanId)
                .DisplayName;
            workspace.PlanNameDraft = duplicatePlanName;
            workspace.ConfirmPlanNameCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            TextBox editor = findRequiredControl<TextBox>(host, "PlanNameEditor");

            Assert.True(workspace.HasPlanNameValidationMessage);
            Assert.Equal(workspace.PlanNameValidationMessage, validationMessage.Text);
            Assert.True(editor.IsKeyboardFocusWithin);
            Assert.Equal(0, editor.SelectionStart);
            int editorTextLength = 0;
            string? editorTextOrNull = editor.Text;
            if (editorTextOrNull != null)
            {
                editorTextLength = editorTextOrNull.Length;
            }

            Assert.Equal(editorTextLength, editor.SelectionEnd);
            Assert.Equal(workspace.PlanNameValidationMessage, AutomationProperties.GetHelpText(editor));
            Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(validationMessage));

            workspace.PlanNameDraft = "고유한 시간표 이름";
            Dispatcher.UIThread.RunJobs();

            Assert.False(workspace.HasPlanNameValidationMessage);
            Assert.Equal(string.Empty, validationMessage.Text);
            Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(validationMessage));

            workspace.PlanNameDraft = duplicatePlanName;
            workspace.ConfirmPlanNameCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(workspace.HasPlanNameValidationMessage);
            Assert.Equal(workspace.PlanNameValidationMessage, validationMessage.Text);
            Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(validationMessage));
            Assert.NotEmpty(liveRegionTransitions);
            Assert.All(
                liveRegionTransitions,
                static transition =>
                {
                    AutomationLiveSetting expectedLiveSetting;
                    if (string.IsNullOrEmpty(transition.Text))
                    {
                        expectedLiveSetting = AutomationLiveSetting.Off;
                    }
                    else
                    {
                        expectedLiveSetting = AutomationLiveSetting.Assertive;
                    }
                    Assert.Equal(expectedLiveSetting, transition.LiveSetting);
                });
        }
        finally
        {
            window.Close();
            workspace.Dispose();
        }
    }

}
