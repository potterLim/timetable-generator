using System.Linq;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView
{
    private Control? mPlanEditingFocusReturnTargetOrNull;

    private bool mWasPlanEditingOverlayVisible;

    private void focusPlanEditingControlWhenRequired()
    {
        if (mWorkspaceOrNull == null || mWorkspaceOrNull.IsPlanEditingOverlayVisible == false)
        {
            return;
        }

        Dispatcher.UIThread.Post(focusPlanEditingControl, DispatcherPriority.Input);
    }

    private void handlePlanEditingOverlayStateChanged()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        bool isOverlayVisible = mWorkspaceOrNull.IsPlanEditingOverlayVisible;
        if (isOverlayVisible && mWasPlanEditingOverlayVisible == false)
        {
            TopLevel? topLevelOrNull = TopLevel.GetTopLevel(this);
            if (topLevelOrNull != null)
            {
                mPlanEditingFocusReturnTargetOrNull = topLevelOrNull.FocusManager?.GetFocusedElement() as Control;
            }
        }

        if (isOverlayVisible == false && mWasPlanEditingOverlayVisible)
        {
            Dispatcher.UIThread.Post(restorePlanEditingFocus, DispatcherPriority.Input);
        }

        mWasPlanEditingOverlayVisible = isOverlayVisible;
        focusPlanEditingControlWhenRequired();
    }

    private void focusPlanEditingControl()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        if (mWorkspaceOrNull.IsPlanNameEditorVisible)
        {
            TextBox? editorOrNull = this.FindControl<TextBox>("PlanNameEditor");
            if (editorOrNull != null)
            {
                editorOrNull.Focus();
                int caretIndex = 0;
                string? editorTextOrNull = editorOrNull.Text;
                if (editorTextOrNull != null)
                {
                    caretIndex = editorTextOrNull.Length;
                }

                editorOrNull.CaretIndex = caretIndex;
                editorOrNull.SelectionStart = caretIndex;
                editorOrNull.SelectionEnd = caretIndex;
            }

            return;
        }

        if (mWorkspaceOrNull.IsDeletePlanConfirmationVisible)
        {
            Button? cancelButtonOrNull = this.FindControl<Button>("CancelDeletePlanButton");
            if (cancelButtonOrNull != null)
            {
                cancelButtonOrNull.Focus();
            }

            return;
        }

        if (mWorkspaceOrNull.IsClearActivePlanConfirmationVisible)
        {
            Button? cancelButtonOrNull = this.FindControl<Button>("CancelClearActivePlanButton");
            if (cancelButtonOrNull != null)
            {
                cancelButtonOrNull.Focus();
            }
        }
    }

    private void restorePlanEditingFocus()
    {
        Control? returnTargetOrNull = mPlanEditingFocusReturnTargetOrNull;
        mPlanEditingFocusReturnTargetOrNull = null;
        if (returnTargetOrNull != null
            && returnTargetOrNull.IsVisible
            && returnTargetOrNull.IsEnabled
            && returnTargetOrNull.IsAttachedToVisualTree()
            && returnTargetOrNull.Focus())
        {
            return;
        }

        Button? planManagementButtonOrNull = this.FindControl<Button>("PlanManagementButton");
        if (planManagementButtonOrNull != null
            && planManagementButtonOrNull.IsEffectivelyVisible
            && planManagementButtonOrNull.Focus())
        {
            return;
        }

        if (focusActivePlanTab())
        {
            return;
        }

        if (mWorkspaceOrNull?.IsWorkspaceEmpty == true)
        {
            focusButton("CreateFirstPlanButton");
            return;
        }

        focusButton("AddPlanButton");
    }

    private void focusPlanNameValidationControlWhenRequired()
    {
        if (mWorkspaceOrNull == null
            || mWorkspaceOrNull.IsPlanNameEditorVisible == false
            || mWorkspaceOrNull.HasPlanNameValidationMessage == false)
        {
            return;
        }

        Dispatcher.UIThread.Post(focusPlanNameValidationControl, DispatcherPriority.Input);
    }

    private void updatePlanNameValidationMessage(PlannerWorkspaceViewModel? workspaceOrNull)
    {
        TextBlock? validationMessageOrNull = this.FindControl<TextBlock>("PlanNameValidationMessage");
        if (validationMessageOrNull == null)
        {
            return;
        }

        string message = string.Empty;
        if (workspaceOrNull != null)
        {
            message = workspaceOrNull.PlanNameValidationMessage;
        }

        AutomationLiveSetting liveSetting = AutomationLiveSetting.Off;
        if (string.IsNullOrEmpty(message) == false)
        {
            liveSetting = AutomationLiveSetting.Assertive;
        }
        AutomationProperties.SetLiveSetting(validationMessageOrNull, liveSetting);
        validationMessageOrNull.Text = message;
    }

    private void focusPlanNameValidationControl()
    {
        TextBox? editorOrNull = this.FindControl<TextBox>("PlanNameEditor");
        if (editorOrNull == null || editorOrNull.Focus() == false)
        {
            return;
        }

        editorOrNull.SelectAll();
    }

    private bool focusActivePlanTab()
    {
        if (mWorkspaceOrNull == null)
        {
            return false;
        }

        PlanTabItem? activePlanOrNull = mWorkspaceOrNull.ActivePlanOrNull;
        if (activePlanOrNull == null)
        {
            return false;
        }

        TabStripItem? activePlanTabOrNull = this.GetVisualDescendants().OfType<TabStripItem>()
            .FirstOrDefault(
                candidate => ReferenceEquals(candidate.DataContext, activePlanOrNull));
        return activePlanTabOrNull != null && activePlanTabOrNull.Focus();
    }
}
