using System.Linq;

using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView
{
    private Control? mPersonalScheduleFocusReturnTargetOrNull;

    private bool mWasPersonalScheduleOverlayVisible;

    private void focusPersonalScheduleControlWhenRequired()
    {
        if (mWorkspaceOrNull == null || mWorkspaceOrNull.IsPersonalScheduleOverlayVisible == false)
        {
            return;
        }

        Dispatcher.UIThread.Post(focusPersonalScheduleControl, DispatcherPriority.Input);
    }

    private void handlePersonalScheduleOverlayStateChanged()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        bool isOverlayVisible = mWorkspaceOrNull.IsPersonalScheduleOverlayVisible;
        if (isOverlayVisible && mWasPersonalScheduleOverlayVisible == false)
        {
            TopLevel? topLevelOrNull = TopLevel.GetTopLevel(this);
            if (topLevelOrNull != null)
            {
                mPersonalScheduleFocusReturnTargetOrNull = topLevelOrNull.FocusManager?.GetFocusedElement() as Control;
            }
        }

        if (isOverlayVisible == false && mWasPersonalScheduleOverlayVisible)
        {
            Dispatcher.UIThread.Post(restorePersonalScheduleFocus, DispatcherPriority.Input);
        }

        mWasPersonalScheduleOverlayVisible = isOverlayVisible;
        focusPersonalScheduleControlWhenRequired();
    }

    private void focusPersonalScheduleControl()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        if (mWorkspaceOrNull.IsPersonalScheduleEditorVisible)
        {
            PersonalScheduleEditorView? editorOrNull = this.FindControl<PersonalScheduleEditorView>("PersonalScheduleEditor");
            if (editorOrNull != null)
            {
                editorOrNull.focusInitialInput();
            }

            return;
        }

        if (mWorkspaceOrNull.IsDeletePersonalScheduleConfirmationVisible)
        {
            Button? cancelButtonOrNull = this.FindControl<Button>("CancelDeletePersonalScheduleButton");
            if (cancelButtonOrNull != null)
            {
                cancelButtonOrNull.Focus();
            }
        }
    }

    private void focusPersonalScheduleValidationControlWhenRequired()
    {
        if (mWorkspaceOrNull == null
            || mWorkspaceOrNull.IsPersonalScheduleEditorVisible == false
            || mWorkspaceOrNull.PersonalScheduleValidationError == EPersonalScheduleDraftValidationError.None)
        {
            return;
        }

        Dispatcher.UIThread.Post(focusPersonalScheduleValidationControl, DispatcherPriority.Input);
    }

    private void focusPersonalScheduleValidationControl()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        PersonalScheduleEditorView? editorOrNull = this.FindControl<PersonalScheduleEditorView>("PersonalScheduleEditor");
        if (editorOrNull != null)
        {
            editorOrNull.focusValidationTarget(mWorkspaceOrNull.PersonalScheduleValidationError);
        }
    }

    private void restorePersonalScheduleFocus()
    {
        Control? returnTargetOrNull = mPersonalScheduleFocusReturnTargetOrNull;
        mPersonalScheduleFocusReturnTargetOrNull = null;
        if (returnTargetOrNull != null
            && returnTargetOrNull.IsVisible
            && returnTargetOrNull.IsEnabled
            && returnTargetOrNull.IsAttachedToVisualTree()
            && returnTargetOrNull.Focus())
        {
            return;
        }

        Button? inspectorAddButtonOrNull = this.FindControl<Button>("AddPersonalScheduleButton");
        if (mWorkspaceOrNull != null
            && mWorkspaceOrNull.IsInspectorPaneOpen
            && inspectorAddButtonOrNull != null
            && inspectorAddButtonOrNull.IsEffectivelyVisible
            && inspectorAddButtonOrNull.Focus())
        {
            return;
        }

        Button? addButtonOrNull = this.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(
                static candidate => candidate.Name
                    == "WorkspaceAddPersonalScheduleButton");
        addButtonOrNull?.Focus();
    }
}
