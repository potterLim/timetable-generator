using System;

using TimetableGenerator.Desktop.Planning;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private void beginCreatePlan()
    {
        throwIfDisposed();
        PlanName availablePlanName = AcademicTermPlanNameFactory.FindAvailablePlanName(mCatalogProjection.Document.Catalog.Term, mSession.Workspace.Plans);
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
                throw new InvalidOperationException("The plan name editor purpose is not supported.");
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
        mSession.AddPlan(PlanId.CreateNew(), newName);
        rebuildPlanItemsAndNotify();
        afterWorkspaceMutation();
    }

    private void confirmRenamePlan(PlanName newName)
    {
        PlanTabItem? planBeingRenamedOrNull = mPlanBeingRenamedOrNull;
        if (planBeingRenamedOrNull == null)
        {
            throw new InvalidOperationException("A rename operation requires a target plan.");
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

    private bool hasOtherPlanWithName(PlanId excludedPlanId, PlanName name)
    {
        foreach (PlanningPlan plan in mSession.Workspace.Plans)
        {
            if (plan.Id != excludedPlanId && StringComparer.OrdinalIgnoreCase.Equals(plan.Name.Value, name.Value))
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
            if (StringComparer.OrdinalIgnoreCase.Equals(plan.Name.Value, name.Value))
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

    private void raisePlanNameEditorStateChanged()
    {
        raisePropertyChanged(nameof(IsPlanNameEditorVisible));
        raisePropertyChanged(nameof(IsCreatingPlan));
        raisePropertyChanged(nameof(IsRenamingPlan));
        raisePropertyChanged(nameof(PlanNameEditorTitle));
        raisePropertyChanged(nameof(PlanNameEditorPrimaryActionText));
        raisePlanEditingStateChanged();
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
        mPlanNameValidationMessage = "시간표 이름은 1~" + PlanName.MAXIMUM_LENGTH + "자로 입력해 주세요.";
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
