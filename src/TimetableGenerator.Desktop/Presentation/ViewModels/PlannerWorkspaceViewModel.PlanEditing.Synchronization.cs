using System;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private void activatePlan(PlanTabItem? planOrNull)
    {
        throwIfDisposed();
        if (planOrNull == null)
        {
            return;
        }

        if (mIsRebuildingPlanItems)
        {
            return;
        }

        if (ReferenceEquals(mActivePlanOrNull, planOrNull))
        {
            return;
        }

        requirePlanItem(planOrNull);
        closePersonalScheduleEditingState();
        closeCourseChoiceEditingState();
        closePlanEditingState();
        mSession.ActivatePlan(planOrNull.PlanId);
        mActivePlanOrNull = planOrNull;
        raisePropertyChanged(nameof(ActivePlan));
        raisePropertyChanged(nameof(ActivePlanOrNull));
        notifyClearActivePlanAvailabilityChanged();
        afterWorkspaceMutation();
    }

    private void closePlanEditingState()
    {
        hidePlanNameEditor();
        clearPlanPendingDeletion();
        clearPlanPendingClear();
        clearPlanNameValidationMessage();
    }

    private void rebuildPlanItemsAndNotify()
    {
        bool previouslyHadActivePlan = mActivePlanOrNull != null;
        rebuildPlanItems();
        mActivePlanOrNull = findPlanItemOrNull(mSession.Workspace.ActivePlanIdOrNull);
        mPlanNameDraft = string.Empty;
        if (mActivePlanOrNull != null)
        {
            mPlanNameDraft = mActivePlanOrNull.DisplayName;
        }

        raisePropertyChanged(nameof(ActivePlan));
        raisePropertyChanged(nameof(ActivePlanOrNull));
        raisePropertyChanged(nameof(HasActivePlan));
        raisePropertyChanged(nameof(IsWorkspaceEmpty));
        raisePropertyChanged(nameof(PlanNameDraft));
        raisePropertyChanged(nameof(CanDeleteActivePlan));
        mBeginDeletePlanCommand.NotifyCanExecuteChanged();
        notifyClearActivePlanAvailabilityChanged();
        updatePaneStateAfterPlanCollectionChanged(previouslyHadActivePlan);
    }

    private void rebuildPlanItems()
    {
        mIsRebuildingPlanItems = true;
        try
        {
            Plans.Clear();
            foreach (PlanningPlan plan in mSession.Workspace.Plans)
            {
                Plans.Add(new PlanTabItem(plan, mCatalogProjection, beginRenamePlan, requestClosePlan));
            }
        }
        finally
        {
            mIsRebuildingPlanItems = false;
        }
    }

    private PlanTabItem? findPlanItemOrNull(PlanId? planIdOrNull)
    {
        if (planIdOrNull.HasValue == false)
        {
            return null;
        }

        foreach (PlanTabItem plan in Plans)
        {
            if (plan.PlanId == planIdOrNull.Value)
            {
                return plan;
            }
        }

        throw new InvalidOperationException("The active planning session plan was not projected for the UI.");
    }

    private PlanTabItem getRequiredActivePlanItem()
    {
        if (mActivePlanOrNull == null)
        {
            throw new InvalidOperationException("The workspace does not currently contain an active plan.");
        }

        return mActivePlanOrNull;
    }

    private void requirePlanItem(PlanTabItem plan)
    {
        foreach (PlanTabItem candidate in Plans)
        {
            if (ReferenceEquals(candidate, plan))
            {
                return;
            }
        }

        throw new ArgumentException("The active plan must belong to this workspace.", nameof(plan));
    }

    private void raisePlanEditingStateChanged()
    {
        raisePropertyChanged(nameof(IsDeletePlanConfirmationVisible));
        raisePropertyChanged(nameof(IsClearActivePlanConfirmationVisible));
        raisePropertyChanged(nameof(IsPlanEditingOverlayVisible));
        raisePropertyChanged(nameof(IsWorkspaceInteractionEnabled));
        raisePropertyChanged(nameof(PlanEditingDialogAccessibleName));
        raisePropertyChanged(nameof(PlanPendingDeletionName));
        raisePropertyChanged(nameof(PlanDeletionDescription));
        raisePropertyChanged(nameof(PlanPendingClearName));
        raisePropertyChanged(nameof(PlanClearDescription));
    }
}
