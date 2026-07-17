using System;
using System.ComponentModel;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView : UserControl
{
    private PlannerWorkspaceViewModel? mWorkspaceOrNull;

    private Control? mPersonalScheduleFocusReturnTargetOrNull;

    private bool mWasPersonalScheduleOverlayVisible;

    private bool mIsAttachedToVisualTree;

    public ProductWorkspaceHostView()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += onDataContextChanged;
        AttachedToVisualTree += onAttachedToVisualTree;
        DetachedFromVisualTree += onDetachedFromVisualTree;
        KeyDown += onKeyDown;
    }

    private void onDataContextChanged(object? senderOrNull, EventArgs eventArgs)
    {
        if (mIsAttachedToVisualTree == false)
        {
            disconnectWorkspace();
            return;
        }

        connectWorkspace(DataContext as PlannerWorkspaceViewModel);
    }

    private void onAttachedToVisualTree(
        object? senderOrNull,
        VisualTreeAttachmentEventArgs eventArgs)
    {
        mIsAttachedToVisualTree = true;
        connectWorkspace(DataContext as PlannerWorkspaceViewModel);
    }

    private void connectWorkspace(PlannerWorkspaceViewModel? workspaceOrNull)
    {
        if (ReferenceEquals(mWorkspaceOrNull, workspaceOrNull))
        {
            return;
        }

        disconnectWorkspace();
        mWorkspaceOrNull = workspaceOrNull;
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        mWorkspaceOrNull.PropertyChanged += onWorkspacePropertyChanged;
        mWasPersonalScheduleOverlayVisible =
            mWorkspaceOrNull.IsPersonalScheduleOverlayVisible;
        focusPlanEditingControlWhenRequired();
        focusPersonalScheduleControlWhenRequired();
    }

    private void disconnectWorkspace()
    {
        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.PropertyChanged -= onWorkspacePropertyChanged;
            mWorkspaceOrNull = null;
        }
    }

    private void onWorkspacePropertyChanged(
        object? senderOrNull,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.IsRenamingPlan))
        {
            focusPlanEditingControlWhenRequired();
        }
        else if (eventArgs.PropertyName
            == nameof(PlannerWorkspaceViewModel.IsDeletePlanConfirmationVisible))
        {
            focusPlanEditingControlWhenRequired();
        }
        else if (eventArgs.PropertyName
            == nameof(PlannerWorkspaceViewModel.IsPersonalScheduleEditorVisible))
        {
            handlePersonalScheduleOverlayStateChanged();
        }
        else if (eventArgs.PropertyName
            == nameof(
                PlannerWorkspaceViewModel.IsDeletePersonalScheduleConfirmationVisible))
        {
            handlePersonalScheduleOverlayStateChanged();
        }
        else if (eventArgs.PropertyName
            == nameof(PlannerWorkspaceViewModel.PersonalScheduleValidationError))
        {
            focusPersonalScheduleValidationControlWhenRequired();
        }
    }

    private void focusPlanEditingControlWhenRequired()
    {
        if (mWorkspaceOrNull == null
            || mWorkspaceOrNull.IsPlanEditingOverlayVisible == false)
        {
            return;
        }

        Dispatcher.UIThread.Post(focusPlanEditingControl, DispatcherPriority.Input);
    }

    private void focusPlanEditingControl()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        if (mWorkspaceOrNull.IsRenamingPlan)
        {
            TextBox? editorOrNull = this.FindControl<TextBox>("PlanNameEditor");
            if (editorOrNull != null)
            {
                editorOrNull.Focus();
                editorOrNull.SelectAll();
            }

            return;
        }

        if (mWorkspaceOrNull.IsDeletePlanConfirmationVisible)
        {
            Button? cancelButtonOrNull = this.FindControl<Button>(
                "CancelDeletePlanButton");
            if (cancelButtonOrNull != null)
            {
                cancelButtonOrNull.Focus();
            }
        }
    }

    private void focusPersonalScheduleControlWhenRequired()
    {
        if (mWorkspaceOrNull == null
            || mWorkspaceOrNull.IsPersonalScheduleOverlayVisible == false)
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
                mPersonalScheduleFocusReturnTargetOrNull =
                    topLevelOrNull.FocusManager?.GetFocusedElement() as Control;
            }
        }

        if (isOverlayVisible == false && mWasPersonalScheduleOverlayVisible)
        {
            Dispatcher.UIThread.Post(
                restorePersonalScheduleFocus,
                DispatcherPriority.Input);
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
            PersonalScheduleEditorView? editorOrNull =
                this.FindControl<PersonalScheduleEditorView>(
                    "PersonalScheduleEditor");
            if (editorOrNull != null)
            {
                editorOrNull.focusInitialInput();
            }

            return;
        }

        if (mWorkspaceOrNull.IsDeletePersonalScheduleConfirmationVisible)
        {
            Button? cancelButtonOrNull = this.FindControl<Button>(
                "CancelDeletePersonalScheduleButton");
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
            || mWorkspaceOrNull.PersonalScheduleValidationError
                == EPersonalScheduleDraftValidationError.None)
        {
            return;
        }

        Dispatcher.UIThread.Post(
            focusPersonalScheduleValidationControl,
            DispatcherPriority.Input);
    }

    private void focusPersonalScheduleValidationControl()
    {
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        PersonalScheduleEditorView? editorOrNull =
            this.FindControl<PersonalScheduleEditorView>(
                "PersonalScheduleEditor");
        if (editorOrNull != null)
        {
            editorOrNull.focusValidationTarget(
                mWorkspaceOrNull.PersonalScheduleValidationError);
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

        Button? addButtonOrNull = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(
                static candidate => candidate.Name
                    == "WorkspaceAddPersonalScheduleButton");
        addButtonOrNull?.Focus();
    }

    private void onKeyDown(object? senderOrNull, KeyEventArgs eventArgs)
    {
        bool isFindShortcut = eventArgs.Key == Key.F
            && (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
                || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta));
        if (isFindShortcut == false)
        {
            return;
        }

        if (mWorkspaceOrNull != null)
        {
            mWorkspaceOrNull.IsCoursePaneOpen = true;
        }

        Dispatcher.UIThread.Post(focusCourseSearchBox, DispatcherPriority.Input);
        eventArgs.Handled = true;
    }

    private void focusCourseSearchBox()
    {
        TextBox? searchBoxOrNull = this.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(
                static candidate => candidate.Name == "CourseSearchBox");
        searchBoxOrNull?.Focus();
    }

    private void onDetachedFromVisualTree(
        object? senderOrNull,
        VisualTreeAttachmentEventArgs eventArgs)
    {
        mIsAttachedToVisualTree = false;
        disconnectWorkspace();
        mPersonalScheduleFocusReturnTargetOrNull = null;
        mWasPersonalScheduleOverlayVisible = false;
    }
}
