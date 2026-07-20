using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using TimetableGenerator.Desktop.Planning;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private readonly DelegateCommand mBeginDeletePlanCommand;

    private readonly DelegateCommand mBeginClearActivePlanCommand;

    private PlanTabItem? mActivePlanOrNull;

    private PlanTabItem? mPlanBeingRenamedOrNull;

    private EPlanNameEditorPurpose? mPlanNameEditorPurposeOrNull;

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

    public bool IsPlanNameEditorVisible
    {
        get
        {
            return mPlanNameEditorPurposeOrNull.HasValue;
        }
    }

    public bool IsCreatingPlan
    {
        get
        {
            return mPlanNameEditorPurposeOrNull
                == EPlanNameEditorPurpose.Create;
        }
    }

    public bool IsRenamingPlan
    {
        get
        {
            return mPlanNameEditorPurposeOrNull
                == EPlanNameEditorPurpose.Rename;
        }
    }

    public string PlanNameEditorTitle
    {
        get
        {
            return mPlanNameEditorPurposeOrNull switch
            {
                EPlanNameEditorPurpose.Create => "시간표 이름",
                EPlanNameEditorPurpose.Rename => "시간표 이름 바꾸기",
                null => string.Empty,
                _ => throw new InvalidOperationException(
                    "The plan name editor purpose is not supported."),
            };
        }
    }

    public string PlanNameEditorPrimaryActionText
    {
        get
        {
            return mPlanNameEditorPurposeOrNull switch
            {
                EPlanNameEditorPurpose.Create => "만들기",
                EPlanNameEditorPurpose.Rename => "저장",
                null => string.Empty,
                _ => throw new InvalidOperationException(
                    "The plan name editor purpose is not supported."),
            };
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
            return IsPlanNameEditorVisible
                || IsDeletePlanConfirmationVisible
                || IsClearActivePlanConfirmationVisible;
        }
    }

    public string PlanEditingDialogAccessibleName
    {
        get
        {
            if (IsPlanNameEditorVisible)
            {
                return PlanNameEditorTitle;
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

    public ICommand ConfirmPlanNameCommand { get; }

    public ICommand CancelPlanNameCommand { get; }

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

    private void beginCreatePlan()
    {
        throwIfDisposed();
        PlanName availablePlanName =
            AcademicTermPlanNameFactory.FindAvailablePlanName(
                mCatalogProjection.Document.Catalog.Term,
                mSession.Workspace.Plans);
        PlanNameDraft = availablePlanName.Value;
        clearPlanNameValidationMessage();
        clearPlanPendingDeletion();
        clearPlanPendingClear();
        showCreatePlanEditor();
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

    private void confirmPlanName()
    {
        throwIfDisposed();
        EPlanNameEditorPurpose? purposeOrNull = mPlanNameEditorPurposeOrNull;
        if (purposeOrNull.HasValue == false)
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

        switch (purposeOrNull.Value)
        {
            case EPlanNameEditorPurpose.Create:
                confirmCreatePlan(newName);
                break;
            case EPlanNameEditorPurpose.Rename:
                confirmRenamePlan(newName);
                break;
            default:
                throw new InvalidOperationException(
                    "The plan name editor purpose is not supported.");
        }
    }

    private void confirmCreatePlan(PlanName newName)
    {
        if (hasPlanWithName(newName))
        {
            showDuplicatePlanNameValidationMessage();
            return;
        }

        hidePlanNameEditor();
        mSession.AddPlan(
            PlanId.CreateNew(),
            newName);
        rebuildPlanItemsAndNotify();
        afterWorkspaceMutation();
    }

    private void confirmRenamePlan(PlanName newName)
    {
        PlanTabItem? planBeingRenamedOrNull = mPlanBeingRenamedOrNull;
        if (planBeingRenamedOrNull == null)
        {
            throw new InvalidOperationException(
                "A rename operation requires a target plan.");
        }

        if (hasOtherPlanWithName(planBeingRenamedOrNull.PlanId, newName))
        {
            showDuplicatePlanNameValidationMessage();
            return;
        }

        if (planBeingRenamedOrNull.Name == newName)
        {
            hidePlanNameEditor();
            return;
        }

        PlanId renamedPlanId = planBeingRenamedOrNull.PlanId;
        hidePlanNameEditor();
        mSession.RenamePlan(renamedPlanId, newName);
        rebuildPlanItemsAndNotify();
        afterWorkspaceMetadataMutation();
    }

    private void cancelPlanNameEditing()
    {
        throwIfDisposed();
        if (IsRenamingPlan && mPlanBeingRenamedOrNull != null)
        {
            PlanNameDraft = mPlanBeingRenamedOrNull.DisplayName;
        }
        else if (mActivePlanOrNull != null)
        {
            PlanNameDraft = mActivePlanOrNull.DisplayName;
        }
        else
        {
            PlanNameDraft = string.Empty;
        }

        clearPlanNameValidationMessage();
        hidePlanNameEditor();
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
        mActivePlanOrNull = findPlanItemOrNull(
            mSession.Workspace.ActivePlanIdOrNull);
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

    private bool hasPlanWithName(PlanName name)
    {
        foreach (PlanningPlan plan in mSession.Workspace.Plans)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(
                plan.Name.Value,
                name.Value))
            {
                return true;
            }
        }

        return false;
    }

    private void showCreatePlanEditor()
    {
        mPlanBeingRenamedOrNull = null;
        mPlanNameEditorPurposeOrNull = EPlanNameEditorPurpose.Create;
        raisePlanNameEditorStateChanged();
    }

    private void showRenamePlanEditor(PlanTabItem plan)
    {
        mPlanBeingRenamedOrNull = plan;
        mPlanNameEditorPurposeOrNull = EPlanNameEditorPurpose.Rename;
        raisePlanNameEditorStateChanged();
    }

    private void hidePlanNameEditor()
    {
        if (mPlanNameEditorPurposeOrNull.HasValue == false)
        {
            return;
        }

        mPlanBeingRenamedOrNull = null;
        mPlanNameEditorPurposeOrNull = null;
        raisePlanNameEditorStateChanged();
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

    private void raisePlanNameEditorStateChanged()
    {
        raisePropertyChanged(nameof(IsPlanNameEditorVisible));
        raisePropertyChanged(nameof(IsCreatingPlan));
        raisePropertyChanged(nameof(IsRenamingPlan));
        raisePropertyChanged(nameof(PlanNameEditorTitle));
        raisePropertyChanged(nameof(PlanNameEditorPrimaryActionText));
        raisePlanEditingStateChanged();
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
        mPlanNameValidationMessage = "시간표 이름은 1~"
            + PlanName.MAXIMUM_LENGTH
            + "자로 입력해 주세요.";
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
