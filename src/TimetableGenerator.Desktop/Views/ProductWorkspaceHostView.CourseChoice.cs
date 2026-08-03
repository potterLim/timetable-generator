using System.Linq;

using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView
{
    private Control? mCourseChoiceFocusReturnTargetOrNull;

    private bool mWasCourseChoiceEditorVisible;

    private void focusCourseChoiceControlWhenRequired()
    {
        if (mWorkspaceOrNull == null || mWorkspaceOrNull.IsCourseChoiceEditorVisible == false)
        {
            return;
        }

        Dispatcher.UIThread.Post(focusCourseChoiceControl, DispatcherPriority.Input);
    }

    private void handleCourseChoiceEditorStateChanged()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        bool isEditorVisible = mWorkspaceOrNull.IsCourseChoiceEditorVisible;
        if (isEditorVisible && mWasCourseChoiceEditorVisible == false)
        {
            TopLevel? topLevelOrNull = TopLevel.GetTopLevel(this);
            if (topLevelOrNull != null)
            {
                mCourseChoiceFocusReturnTargetOrNull = topLevelOrNull.FocusManager?.GetFocusedElement() as Control;
            }
        }

        if (isEditorVisible == false && mWasCourseChoiceEditorVisible)
        {
            Dispatcher.UIThread.Post(restoreCourseChoiceFocus, DispatcherPriority.Input);
        }

        mWasCourseChoiceEditorVisible = isEditorVisible;
        focusCourseChoiceControlWhenRequired();
    }

    private void focusCourseChoiceControl()
    {
        CourseChoiceEditorView? editorOrNull = this.FindControl<CourseChoiceEditorView>("CourseChoiceEditor");
        editorOrNull?.focusInitialInput();
    }

    private void restoreCourseChoiceFocus()
    {
        Control? returnTargetOrNull = mCourseChoiceFocusReturnTargetOrNull;
        mCourseChoiceFocusReturnTargetOrNull = null;
        if (returnTargetOrNull != null
            && returnTargetOrNull.IsVisible
            && returnTargetOrNull.IsEnabled
            && returnTargetOrNull.IsAttachedToVisualTree()
            && returnTargetOrNull.Focus())
        {
            return;
        }

        Button? editButtonOrNull = this.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(
                static candidate => candidate.Name
                    == "CourseChoiceGroupEditButton"
                    || candidate.Name == "AlternativeCourseChoiceGroupEditButton");
        if (editButtonOrNull != null && editButtonOrNull.Focus())
        {
            return;
        }

        focusCourseSearchBox();
    }
}
