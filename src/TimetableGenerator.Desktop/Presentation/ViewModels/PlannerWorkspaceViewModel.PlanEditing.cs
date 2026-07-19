using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using TimetableGenerator.Desktop.Planning;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private readonly DelegateCommand mConfirmRenamePlanCommand;

    private readonly DelegateCommand mBeginDeletePlanCommand;

    private readonly DelegateCommand mBeginClearActivePlanCommand;

    private PlanTabItem? mActivePlanOrNull;

    private PlanTabItem? mPlanBeingRenamedOrNull;

    private PlanTabItem? mPlanPendingDeletionOrNull;

    private PlanTabItem? mPlanPendingClearOrNull;

    private string mPlanNameDraft;

    private string mPlanNameValidationMessage;

    private bool mIsRebuildingPlanItems;

    public ObservableCollection<PlanTabItem> Plans { get; }

    public PlanTabItem ActivePlan
    {
        get
        {
            return getRequiredActivePlanItem();
        }
        set
        {
            activatePlan(value);
        }
    }

    public PlanTabItem? ActivePlanOrNull
    {
        get
        {
            return mActivePlanOrNull;
        }
        set
        {
            activatePlan(value);
        }
    }

    public bool HasActivePlan
    {
        get
        {
            return mActivePlanOrNull != null;
        }
    }

    public bool IsWorkspaceEmpty
    {
        get
        {
            return HasActivePlan == false;
        }
    }

    public bool IsRenamingPlan
    {
        get
        {
            return mPlanBeingRenamedOrNull != null;
        }
    }

    public bool IsDeletePlanConfirmationVisible
    {
        get
        {
            return mPlanPendingDeletionOrNull != null;
        }
    }

    public bool IsClearActivePlanConfirmationVisible
    {
        get
        {
            return mPlanPendingClearOrNull != null;
        }
    }

    public bool IsPlanEditingOverlayVisible
    {
        get
        {
            return IsRenamingPlan
                || IsDeletePlanConfirmationVisible
                || IsClearActivePlanConfirmationVisible;
        }
    }

    public string PlanEditingDialogAccessibleName
    {
        get
        {
            if (IsRenamingPlan)
            {
                return "시간표 이름 바꾸기";
            }

            if (IsDeletePlanConfirmationVisible)
            {
                return "시간표 삭제 확인";
            }

            if (IsClearActivePlanConfirmationVisible)
            {
                return "시간표 비우기 확인";
            }

            return "시간표 편집";
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

            return "삭제할 시간표: '"
                + mPlanPendingDeletionOrNull.DisplayName
                + "'";
        }
    }

    public string PlanPendingClearName
    {
        get
        {
            if (mPlanPendingClearOrNull == null)
            {
                return string.Empty;
            }

            return mPlanPendingClearOrNull.DisplayName;
        }
    }

    public string PlanClearDescription
    {
        get
        {
            if (mPlanPendingClearOrNull == null)
            {
                return string.Empty;
            }

            return "'" + mPlanPendingClearOrNull.DisplayName
                + "'의 모든 내용을 지웁니다.";
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
            return HasActivePlan;
        }
    }

    public bool CanClearActivePlan
    {
        get
        {
            return mActivePlanOrNull != null
                && mActivePlanOrNull.IsCompletelyEmpty == false;
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

    public ICommand BeginClearActivePlanCommand
    {
        get
        {
            return mBeginClearActivePlanCommand;
        }
    }

    public ICommand ConfirmClearActivePlanCommand { get; }

    public ICommand CancelClearActivePlanCommand { get; }

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

    private void addPlan()
    {
        throwIfDisposed();
        PlanName availablePlanName =
            AcademicTermPlanNameFactory.FindAvailablePlanName(
                mCatalogProjection.Document.Catalog.Term,
                mSession.Workspace.Plans);
        mSession.AddPlan(
            PlanId.CreateNew(),
            availablePlanName);
        rebuildPlanItemsAndNotify();
        beginRenamePlan();
        afterWorkspaceMutation();
    }

    private void beginRenamePlan()
    {
        if (mActivePlanOrNull != null)
        {
            beginRenamePlan(mActivePlanOrNull);
        }
    }

    private void beginRenamePlan(PlanTabItem plan)
    {
        throwIfDisposed();
        requirePlanItem(plan);
        PlanNameDraft = plan.DisplayName;
        clearPlanNameValidationMessage();
        clearPlanPendingDeletion();
        clearPlanPendingClear();
        showRenamePlanEditor(plan);
    }

    private void confirmRenamePlan()
    {
        throwIfDisposed();
        PlanTabItem? planBeingRenamedOrNull = mPlanBeingRenamedOrNull;
        if (planBeingRenamedOrNull == null)
        {
            return;
        }

        PlanName newName;
        try
        {
            newName = new PlanName(PlanNameDraft);
        }
        catch (ArgumentException)
        {
            showInvalidPlanNameValidationMessage();
            return;
        }

        if (hasOtherPlanWithName(planBeingRenamedOrNull.PlanId, newName))
        {
            showDuplicatePlanNameValidationMessage();
            return;
        }

        if (planBeingRenamedOrNull.Name == newName)
        {
            hideRenamePlanEditor();
            return;
        }

        PlanId renamedPlanId = planBeingRenamedOrNull.PlanId;
        hideRenamePlanEditor();
        mSession.RenamePlan(renamedPlanId, newName);
        rebuildPlanItemsAndNotify();
        afterWorkspaceMetadataMutation();
    }

    private void cancelRenamePlan()
    {
        if (mPlanBeingRenamedOrNull != null)
        {
            PlanNameDraft = mPlanBeingRenamedOrNull.DisplayName;
        }

        clearPlanNameValidationMessage();
        hideRenamePlanEditor();
    }

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
        hideRenamePlanEditor();
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

        hideRenamePlanEditor();
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

    private void closePlanEditingState()
    {
        hideRenamePlanEditor();
        clearPlanPendingDeletion();
        clearPlanPendingClear();
        clearPlanNameValidationMessage();
    }

    private void rebuildPlanItemsAndNotify()
    {
        bool previouslyHadActivePlan = mActivePlanOrNull != null;
        rebuildPlanItems();
        mActivePlanOrNull = findPlanItemOrNull(
            mSession.Workspace.ActivePlanIdOrNull);
        mPlanNameDraft = mActivePlanOrNull?.DisplayName ?? string.Empty;
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
                Plans.Add(new PlanTabItem(
                    plan,
                    mCatalogProjection,
                    beginRenamePlan,
                    requestClosePlan));
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

        throw new InvalidOperationException(
            "The active planning session plan was not projected for the UI.");
    }

    private PlanTabItem getRequiredActivePlanItem()
    {
        if (mActivePlanOrNull == null)
        {
            throw new InvalidOperationException(
                "The workspace does not currently contain an active plan.");
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

        throw new ArgumentException(
            "The active plan must belong to this workspace.",
            nameof(plan));
    }

    private bool hasOtherPlanWithName(PlanId excludedPlanId, PlanName name)
    {
        foreach (PlanningPlan plan in mSession.Workspace.Plans)
        {
            if (plan.Id != excludedPlanId
                && StringComparer.OrdinalIgnoreCase.Equals(
                    plan.Name.Value,
                    name.Value))
            {
                return true;
            }
        }

        return false;
    }

    private void showRenamePlanEditor(PlanTabItem plan)
    {
        mPlanBeingRenamedOrNull = plan;
        raisePropertyChanged(nameof(IsRenamingPlan));
        raisePlanEditingStateChanged();
    }

    private void hideRenamePlanEditor()
    {
        if (mPlanBeingRenamedOrNull == null)
        {
            return;
        }

        mPlanBeingRenamedOrNull = null;
        raisePropertyChanged(nameof(IsRenamingPlan));
        raisePlanEditingStateChanged();
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

    private void showInvalidPlanNameValidationMessage()
    {
        mPlanNameValidationMessage = "시간표 이름은 1~80자로 입력해 주세요.";
        raisePropertyChanged(nameof(PlanNameValidationMessage));
        raisePropertyChanged(nameof(HasPlanNameValidationMessage));
    }

    private void showDuplicatePlanNameValidationMessage()
    {
        mPlanNameValidationMessage = "같은 이름의 시간표가 이미 있습니다.";
        raisePropertyChanged(nameof(PlanNameValidationMessage));
        raisePropertyChanged(nameof(HasPlanNameValidationMessage));
    }
}
