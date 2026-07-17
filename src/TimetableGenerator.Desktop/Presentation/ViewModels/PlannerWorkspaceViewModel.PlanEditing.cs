using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private readonly DelegateCommand mConfirmRenamePlanCommand;

    private readonly DelegateCommand mBeginDeletePlanCommand;

    private PlanTabItem mActivePlan;

    private bool mIsRenamingPlan;

    private PlanTabItem? mPlanPendingDeletionOrNull;

    private string mPlanNameDraft;

    private string mPlanNameValidationMessage;

    private bool mIsRebuildingPlanItems;

    public ObservableCollection<PlanTabItem> Plans { get; }

    public PlanTabItem ActivePlan
    {
        get
        {
            return mActivePlan;
        }
        set
        {
            activatePlan(value);
        }
    }

    public bool IsRenamingPlan
    {
        get
        {
            return mIsRenamingPlan;
        }
    }

    public bool IsDeletePlanConfirmationVisible
    {
        get
        {
            return mPlanPendingDeletionOrNull != null;
        }
    }

    public bool IsPlanEditingOverlayVisible
    {
        get
        {
            return IsRenamingPlan || IsDeletePlanConfirmationVisible;
        }
    }

    public bool IsWorkspaceInteractionEnabled
    {
        get
        {
            return IsPlanEditingOverlayVisible == false
                && IsPersonalScheduleOverlayVisible == false
                && IsCourseChoiceEditorVisible == false;
        }
    }

    public string PlanPendingDeletionName
    {
        get
        {
            if (mPlanPendingDeletionOrNull == null)
            {
                return string.Empty;
            }

            return mPlanPendingDeletionOrNull.DisplayName;
        }
    }

    public string PlanDeletionDescription
    {
        get
        {
            if (mPlanPendingDeletionOrNull == null)
            {
                return string.Empty;
            }

            return "‘" + mPlanPendingDeletionOrNull.DisplayName
                + "’의 과목, 개인 일정, 추천 결과가 모두 삭제됩니다.";
        }
    }

    public string PlanNameDraft
    {
        get
        {
            return mPlanNameDraft;
        }
        set
        {
            string normalizedValue = value;
            if (normalizedValue == null)
            {
                normalizedValue = string.Empty;
            }

            if (setProperty(ref mPlanNameDraft, normalizedValue))
            {
                clearPlanNameValidationMessage();
            }
        }
    }

    public string PlanNameValidationMessage
    {
        get
        {
            return mPlanNameValidationMessage;
        }
    }

    public bool HasPlanNameValidationMessage
    {
        get
        {
            return string.IsNullOrEmpty(PlanNameValidationMessage) == false;
        }
    }

    public bool CanDeleteActivePlan
    {
        get
        {
            return Plans.Count > 1;
        }
    }

    public ICommand AddPlanCommand { get; }

    public ICommand BeginRenamePlanCommand { get; }

    public ICommand ConfirmRenamePlanCommand
    {
        get
        {
            return mConfirmRenamePlanCommand;
        }
    }

    public ICommand CancelRenamePlanCommand { get; }

    public ICommand BeginDeletePlanCommand
    {
        get
        {
            return mBeginDeletePlanCommand;
        }
    }

    public ICommand ConfirmDeletePlanCommand { get; }

    public ICommand CancelDeletePlanCommand { get; }

    private void activatePlan(PlanTabItem plan)
    {
        throwIfDisposed();
        if (plan == null)
        {
            return;
        }

        if (mIsRebuildingPlanItems)
        {
            return;
        }

        if (ReferenceEquals(mActivePlan, plan))
        {
            return;
        }

        requirePlanItem(plan);
        closePersonalScheduleEditingState();
        closeCourseChoiceEditingState();
        closePlanEditingState();
        mSession.ActivatePlan(plan.PlanId);
        mActivePlan = plan;
        raisePropertyChanged(nameof(ActivePlan));
        afterWorkspaceMutation();
    }

    private void addPlan()
    {
        throwIfDisposed();
        int nextPlanNumber = Plans.Count + 1;
        mSession.AddPlan(
            PlanId.CreateNew(),
            new PlanName("새 계획 " + nextPlanNumber));
        rebuildPlanItemsAndNotify();
        beginRenamePlan();
        afterWorkspaceMutation();
    }

    private void beginRenamePlan()
    {
        PlanNameDraft = ActivePlan.DisplayName;
        clearPlanNameValidationMessage();
        clearPlanPendingDeletion();
        showRenamePlanEditor();
    }

    private void confirmRenamePlan()
    {
        throwIfDisposed();
        if (IsRenamingPlan == false)
        {
            return;
        }

        try
        {
            PlanName newName = new PlanName(PlanNameDraft);
            mSession.RenamePlan(ActivePlan.PlanId, newName);
            rebuildPlanItemsAndNotify();
            hideRenamePlanEditor();
            afterWorkspaceMutation();
        }
        catch (ArgumentException)
        {
            mPlanNameValidationMessage = "계획 이름은 1~80자로 입력해 주세요.";
            raisePropertyChanged(nameof(PlanNameValidationMessage));
            raisePropertyChanged(nameof(HasPlanNameValidationMessage));
        }
    }

    private void cancelRenamePlan()
    {
        PlanNameDraft = ActivePlan.DisplayName;
        clearPlanNameValidationMessage();
        hideRenamePlanEditor();
    }

    private void beginDeletePlan()
    {
        requestClosePlan(ActivePlan);
    }

    private void requestClosePlan(PlanTabItem plan)
    {
        throwIfDisposed();
        requirePlanItem(plan);
        if (Plans.Count <= 1)
        {
            return;
        }

        hideRenamePlanEditor();
        mPlanPendingDeletionOrNull = plan;
        raisePlanEditingStateChanged();
    }

    private void confirmDeletePlan()
    {
        throwIfDisposed();
        PlanTabItem? planPendingDeletionOrNull = mPlanPendingDeletionOrNull;
        if (planPendingDeletionOrNull == null || Plans.Count <= 1)
        {
            return;
        }

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

    private void closePlanEditingState()
    {
        hideRenamePlanEditor();
        clearPlanPendingDeletion();
        clearPlanNameValidationMessage();
    }

    private void rebuildPlanItemsAndNotify()
    {
        rebuildPlanItems();
        mActivePlan = findPlanItem(mSession.Workspace.ActivePlanId);
        mPlanNameDraft = mActivePlan.DisplayName;
        raisePropertyChanged(nameof(ActivePlan));
        raisePropertyChanged(nameof(PlanNameDraft));
        raisePropertyChanged(nameof(CanDeleteActivePlan));
        mBeginDeletePlanCommand.NotifyCanExecuteChanged();
    }

    private void rebuildPlanItems()
    {
        EPlanCloseAvailability closeAvailability =
            EPlanCloseAvailability.Unavailable;
        if (mSession.Workspace.Plans.Count > 1)
        {
            closeAvailability = EPlanCloseAvailability.Available;
        }

        mIsRebuildingPlanItems = true;
        try
        {
            Plans.Clear();
            foreach (PlanningPlan plan in mSession.Workspace.Plans)
            {
                Plans.Add(new PlanTabItem(
                    plan,
                    mCatalogProjection,
                    closeAvailability,
                    requestClosePlan));
            }
        }
        finally
        {
            mIsRebuildingPlanItems = false;
        }
    }

    private PlanTabItem findPlanItem(PlanId planId)
    {
        foreach (PlanTabItem plan in Plans)
        {
            if (plan.PlanId == planId)
            {
                return plan;
            }
        }

        throw new InvalidOperationException(
            "The active planning session plan was not projected for the UI.");
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

        throw new ArgumentException(
            "The active plan must belong to this workspace.",
            nameof(plan));
    }

    private void showRenamePlanEditor()
    {
        if (setProperty(ref mIsRenamingPlan, true, nameof(IsRenamingPlan)))
        {
            raisePlanEditingStateChanged();
        }
    }

    private void hideRenamePlanEditor()
    {
        if (setProperty(ref mIsRenamingPlan, false, nameof(IsRenamingPlan)))
        {
            raisePlanEditingStateChanged();
        }
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

    private void raisePlanEditingStateChanged()
    {
        raisePropertyChanged(nameof(IsDeletePlanConfirmationVisible));
        raisePropertyChanged(nameof(IsPlanEditingOverlayVisible));
        raisePropertyChanged(nameof(IsWorkspaceInteractionEnabled));
        raisePropertyChanged(nameof(PlanPendingDeletionName));
        raisePropertyChanged(nameof(PlanDeletionDescription));
    }

    private void clearPlanNameValidationMessage()
    {
        if (string.IsNullOrEmpty(mPlanNameValidationMessage))
        {
            return;
        }

        mPlanNameValidationMessage = string.Empty;
        raisePropertyChanged(nameof(PlanNameValidationMessage));
        raisePropertyChanged(nameof(HasPlanNameValidationMessage));
    }
}
