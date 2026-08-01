using TimetableGenerator.Desktop.Presentation.Models;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private void beginDeletePlan()
    {
        if (mActivePlanOrNull != null)
        {
            requestClosePlan(mActivePlanOrNull);
        }
    }

    private void requestClosePlan(PlanTabItem plan)
    {
        throwIfDisposed();
        requirePlanItem(plan);
        hidePlanNameEditor();
        clearPlanPendingClear();
        mPlanPendingDeletionOrNull = plan;
        raisePlanEditingStateChanged();
    }

    private void confirmDeletePlan()
    {
        throwIfDisposed();
        PlanTabItem? planPendingDeletionOrNull = mPlanPendingDeletionOrNull;
        if (planPendingDeletionOrNull == null)
        {
            return;
        }

        closePersonalScheduleEditingState();
        closeCourseChoiceEditingState();
        mSession.RemovePlan(planPendingDeletionOrNull.PlanId);
        clearPlanPendingDeletion();
        rebuildPlanItemsAndNotify();
        afterWorkspaceMutation();
    }

    private void cancelDeletePlan()
    {
        clearPlanPendingDeletion();
    }

    private bool canDeletePlan()
    {
        return CanDeleteActivePlan;
    }

    private void beginClearActivePlan()
    {
        throwIfDisposed();
        if (CanClearActivePlan == false)
        {
            return;
        }

        hidePlanNameEditor();
        clearPlanPendingDeletion();
        mPlanPendingClearOrNull = getRequiredActivePlanItem();
        raisePlanEditingStateChanged();
    }

    private void confirmClearActivePlan()
    {
        throwIfDisposed();
        PlanTabItem? planPendingClearOrNull = mPlanPendingClearOrNull;
        if (planPendingClearOrNull == null
            || mActivePlanOrNull == null
            || planPendingClearOrNull.PlanId != mActivePlanOrNull.PlanId)
        {
            return;
        }

        mSession.ClearActivePlanContent();
        clearPlanPendingClear();
        afterPlanContentMutation();
    }

    private void cancelClearActivePlan()
    {
        clearPlanPendingClear();
    }

    private bool canClearActivePlan()
    {
        return CanClearActivePlan;
    }

    private void clearPlanPendingDeletion()
    {
        if (mPlanPendingDeletionOrNull == null)
        {
            return;
        }

        mPlanPendingDeletionOrNull = null;
        raisePlanEditingStateChanged();
    }

    private void clearPlanPendingClear()
    {
        if (mPlanPendingClearOrNull == null)
        {
            return;
        }

        mPlanPendingClearOrNull = null;
        raisePlanEditingStateChanged();
    }

    private void notifyClearActivePlanAvailabilityChanged()
    {
        raisePropertyChanged(nameof(CanClearActivePlan));
        mBeginClearActivePlanCommand.NotifyCanExecuteChanged();
    }
}
