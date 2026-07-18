using System;
using System.ComponentModel;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

    private Control? mPlanEditingFocusReturnTargetOrNull;

    private bool mWasPlanEditingOverlayVisible;

    private Control? mPersonalScheduleFocusReturnTargetOrNull;

    private bool mWasPersonalScheduleOverlayVisible;

    private Control? mCourseChoiceFocusReturnTargetOrNull;

    private bool mWasCourseChoiceEditorVisible;

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
        mWasPlanEditingOverlayVisible =
            mWorkspaceOrNull.IsPlanEditingOverlayVisible;
        mWasPersonalScheduleOverlayVisible =
            mWorkspaceOrNull.IsPersonalScheduleOverlayVisible;
        mWasCourseChoiceEditorVisible =
            mWorkspaceOrNull.IsCourseChoiceEditorVisible;
        focusPlanEditingControlWhenRequired();
        focusPersonalScheduleControlWhenRequired();
        focusCourseChoiceControlWhenRequired();
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
        if (eventArgs.PropertyName
            == nameof(PlannerWorkspaceViewModel.IsPlanEditingOverlayVisible))
        {
            handlePlanEditingOverlayStateChanged();
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
        else if (eventArgs.PropertyName
            == nameof(PlannerWorkspaceViewModel.IsCourseChoiceEditorVisible))
        {
            handleCourseChoiceEditorStateChanged();
        }
        else if (eventArgs.PropertyName
            == nameof(PlannerWorkspaceViewModel.IsCoursePaneOpen))
        {
            handleCoursePaneOpenStateChanged();
        }
        else if (eventArgs.PropertyName
            == nameof(PlannerWorkspaceViewModel.IsInspectorPaneOpen))
        {
            handleInspectorPaneOpenStateChanged();
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
                mPlanEditingFocusReturnTargetOrNull =
                    topLevelOrNull.FocusManager?.GetFocusedElement() as Control;
            }
        }

        if (isOverlayVisible == false && mWasPlanEditingOverlayVisible)
        {
            Dispatcher.UIThread.Post(
                restorePlanEditingFocus,
                DispatcherPriority.Input);
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

            return;
        }

        if (mWorkspaceOrNull.IsClearActivePlanConfirmationVisible)
        {
            Button? cancelButtonOrNull = this.FindControl<Button>(
                "CancelClearActivePlanButton");
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

        Button? planManagementButtonOrNull = this.FindControl<Button>(
            "PlanManagementButton");
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

        focusButton("AddPlanButton");
    }

    private bool focusActivePlanTab()
    {
        if (mWorkspaceOrNull == null)
        {
            return false;
        }

        TabStripItem? activePlanTabOrNull = this.GetVisualDescendants()
            .OfType<TabStripItem>()
            .FirstOrDefault(
                candidate => ReferenceEquals(
                    candidate.DataContext,
                    mWorkspaceOrNull.ActivePlan));
        return activePlanTabOrNull != null && activePlanTabOrNull.Focus();
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

        Button? inspectorAddButtonOrNull = this.FindControl<Button>(
            "AddPersonalScheduleButton");
        if (mWorkspaceOrNull != null
            && mWorkspaceOrNull.IsInspectorPaneOpen
            && inspectorAddButtonOrNull != null
            && inspectorAddButtonOrNull.IsEffectivelyVisible
            && inspectorAddButtonOrNull.Focus())
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

    private void focusCourseChoiceControlWhenRequired()
    {
        if (mWorkspaceOrNull == null
            || mWorkspaceOrNull.IsCourseChoiceEditorVisible == false)
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
                mCourseChoiceFocusReturnTargetOrNull =
                    topLevelOrNull.FocusManager?.GetFocusedElement() as Control;
            }
        }

        if (isEditorVisible == false && mWasCourseChoiceEditorVisible)
        {
            Dispatcher.UIThread.Post(
                restoreCourseChoiceFocus,
                DispatcherPriority.Input);
        }

        mWasCourseChoiceEditorVisible = isEditorVisible;
        focusCourseChoiceControlWhenRequired();
    }

    private void focusCourseChoiceControl()
    {
        CourseChoiceEditorView? editorOrNull =
            this.FindControl<CourseChoiceEditorView>("CourseChoiceEditor");
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

        Button? editButtonOrNull = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(
                static candidate => candidate.Name
                    == "CourseChoiceGroupEditButton"
                    || candidate.Name
                    == "AlternativeCourseChoiceGroupEditButton");
        if (editButtonOrNull != null && editButtonOrNull.Focus())
        {
            return;
        }

        focusCourseSearchBox();
    }

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

        Button? openActionOrNull = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(
                candidate => ReferenceEquals(
                    candidate.Command,
                    mWorkspaceOrNull.ToggleCoursePaneCommand)
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
            Dispatcher.UIThread.Post(
                focusInspectorPaneDismissAction,
                DispatcherPriority.Input);
            return;
        }

        Dispatcher.UIThread.Post(
            focusInspectorPaneOpenAction,
            DispatcherPriority.Input);
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
        Button? buttonOrNull = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(candidate => candidate.Name == buttonName);
        buttonOrNull?.Focus();
    }

    private void onKeyDown(object? senderOrNull, KeyEventArgs eventArgs)
    {
        if (mWorkspaceOrNull != null
            && mWorkspaceOrNull.IsCourseChoiceEditorVisible)
        {
            if (eventArgs.Key == Key.Escape)
            {
                mWorkspaceOrNull.closeOverlayPanes();
                eventArgs.Handled = true;
            }

            return;
        }

        bool isFindShortcut = eventArgs.Key == Key.F
            && (eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
                || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta));
        if (isFindShortcut == false)
        {
            return;
        }

        if (mWorkspaceOrNull != null)
        {
            if (mWorkspaceOrNull.IsCoursePaneOpen == false)
            {
                mWorkspaceOrNull.ToggleCoursePaneCommand.Execute(null);
            }
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
        mPlanEditingFocusReturnTargetOrNull = null;
        mWasPlanEditingOverlayVisible = false;
        mPersonalScheduleFocusReturnTargetOrNull = null;
        mWasPersonalScheduleOverlayVisible = false;
        mCourseChoiceFocusReturnTargetOrNull = null;
        mWasCourseChoiceEditorVisible = false;
    }
}
