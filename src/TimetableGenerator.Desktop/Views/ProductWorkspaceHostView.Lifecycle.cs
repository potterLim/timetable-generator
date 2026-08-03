using System;
using System.ComponentModel;

using Avalonia;

using TimetableGenerator.Desktop.Presentation.ViewModels;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ProductWorkspaceHostView
{
    private PlannerWorkspaceViewModel? mWorkspaceOrNull;

    private bool mIsAttachedToVisualTree;

    private void onDataContextChanged(object? senderOrNull, EventArgs eventArgs)
    {
        if (mIsAttachedToVisualTree == false)
        {
            disconnectWorkspace();
            return;
        }

        connectWorkspace(DataContext as PlannerWorkspaceViewModel);
    }

    private void onAttachedToVisualTree(object? senderOrNull, VisualTreeAttachmentEventArgs eventArgs)
    {
        mIsAttachedToVisualTree = true;
        connectWorkspace(DataContext as PlannerWorkspaceViewModel);
    }

    private void connectWorkspace(PlannerWorkspaceViewModel? workspaceOrNull)
    {
        if (ReferenceEquals(mWorkspaceOrNull, workspaceOrNull))
        {
            updatePlanNameValidationMessage(workspaceOrNull);
            return;
        }

        disconnectWorkspace();
        mWorkspaceOrNull = workspaceOrNull;
        updatePlanNameValidationMessage(mWorkspaceOrNull);
        if (mWorkspaceOrNull == null)
        {
            return;
        }

        mWorkspaceOrNull.PropertyChanged += onWorkspacePropertyChanged;
        mWasPlanEditingOverlayVisible = mWorkspaceOrNull.IsPlanEditingOverlayVisible;
        mWasPersonalScheduleOverlayVisible = mWorkspaceOrNull.IsPersonalScheduleOverlayVisible;
        mWasCourseChoiceEditorVisible = mWorkspaceOrNull.IsCourseChoiceEditorVisible;
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

    private void onWorkspacePropertyChanged(object? senderOrNull, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.IsPlanEditingOverlayVisible))
        {
            handlePlanEditingOverlayStateChanged();
        }
        else if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.PlanNameValidationMessage))
        {
            updatePlanNameValidationMessage(mWorkspaceOrNull);
            focusPlanNameValidationControlWhenRequired();
        }
        else if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.IsPersonalScheduleEditorVisible))
        {
            handlePersonalScheduleOverlayStateChanged();
        }
        else if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.IsDeletePersonalScheduleConfirmationVisible))
        {
            handlePersonalScheduleOverlayStateChanged();
        }
        else if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.PersonalScheduleValidationError))
        {
            focusPersonalScheduleValidationControlWhenRequired();
        }
        else if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.IsCourseChoiceEditorVisible))
        {
            handleCourseChoiceEditorStateChanged();
        }
        else if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.IsCoursePaneOpen))
        {
            handleCoursePaneOpenStateChanged();
        }
        else if (eventArgs.PropertyName == nameof(PlannerWorkspaceViewModel.IsInspectorPaneOpen))
        {
            handleInspectorPaneOpenStateChanged();
        }
    }

    private void onDetachedFromVisualTree(object? senderOrNull, VisualTreeAttachmentEventArgs eventArgs)
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
