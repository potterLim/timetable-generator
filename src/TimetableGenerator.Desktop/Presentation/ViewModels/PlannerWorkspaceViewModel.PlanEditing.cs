using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

using TimetableGenerator.Desktop.Presentation.Models;

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
            return mPlanNameEditorPurposeOrNull == EPlanNameEditorPurpose.Create;
        }
    }

    public bool IsRenamingPlan
    {
        get
        {
            return mPlanNameEditorPurposeOrNull == EPlanNameEditorPurpose.Rename;
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
                _ => throw new InvalidOperationException("The plan name editor purpose is not supported."),
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
                _ => throw new InvalidOperationException("The plan name editor purpose is not supported."),
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

            return "삭제할 시간표: '" + mPlanPendingDeletionOrNull.DisplayName + "'";
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

            return "'" + mPlanPendingClearOrNull.DisplayName + "'의 모든 내용을 지웁니다.";
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
            return mActivePlanOrNull != null && mActivePlanOrNull.IsCompletelyEmpty == false;
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
}
