using System.Linq;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView
{
    private void handleCoursePaneOpenStateChanged()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        if (mWorkspaceOrNull.IsCoursePaneOpen)
        {
            Dispatcher.UIThread.Post(focusCourseSearchBox, DispatcherPriority.Input);
            return;
        }

        Dispatcher.UIThread.Post(focusCoursePaneOpenAction, DispatcherPriority.Input);
    }

    private void focusCoursePaneOpenAction()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        Button? openActionOrNull = this.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(
                candidate => ReferenceEquals(candidate.Command, mWorkspaceOrNull.ToggleCoursePaneCommand)
                    && candidate.IsEffectivelyVisible
                    && candidate.Name != "CloseCoursePaneButton");
        openActionOrNull?.Focus();
    }

    private void handleInspectorPaneOpenStateChanged()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        if (mWorkspaceOrNull.IsInspectorPaneOpen)
        {
            Dispatcher.UIThread.Post(focusInspectorPaneDismissAction, DispatcherPriority.Input);
            return;
        }

        Dispatcher.UIThread.Post(focusInspectorPaneOpenAction, DispatcherPriority.Input);
    }

    private void focusInspectorPaneDismissAction()
    {
        focusButton("CloseInspectorPaneButton");
    }

    private void focusInspectorPaneOpenAction()
    {
        focusButton("OpenInspectorPaneButton");
    }

    private void focusButton(string buttonName)
    {
        Button? buttonOrNull = this.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(candidate => candidate.Name == buttonName);
        buttonOrNull?.Focus();
    }

    private void onKeyDown(object? senderOrNull, KeyEventArgs eventArgs)
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        if (eventArgs.Key == Key.Escape)
        {
            eventArgs.Handled = mWorkspaceOrNull.tryCloseTopmostTransientWorkspaceOverlay();
            return;
        }

        if (mWorkspaceOrNull.IsWorkspaceInteractionEnabled == false)
        {
            return;
        }

        bool isFindShortcut = eventArgs.Key == Key.F
            && (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
                || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta));
        if (isFindShortcut == false)
        {
            return;
        }

        if (mWorkspaceOrNull.IsCoursePaneOpen == false)
        {
            mWorkspaceOrNull.ToggleCoursePaneCommand.Execute(null);
        }

        Dispatcher.UIThread.Post(focusCourseSearchBox, DispatcherPriority.Input);
        eventArgs.Handled = true;
    }

    private void focusCourseSearchBox()
    {
        TextBox? searchBoxOrNull = this.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(
                static candidate => candidate.Name == "CourseSearchBox");
        searchBoxOrNull?.Focus();
    }
}
