using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private static readonly ScheduleTime DEFAULT_PERSONAL_SCHEDULE_START_TIME =
        new ScheduleTime(12, 0);

    private static readonly ScheduleTime DEFAULT_PERSONAL_SCHEDULE_END_TIME =
        new ScheduleTime(13, 0);

    private readonly IReadOnlyList<PersonalScheduleDayOption>
        mPersonalScheduleDayOptions;

    private PersonalScheduleId? mEditingPersonalScheduleIdOrNull;

    private PersonalScheduleItem? mPersonalSchedulePendingDeletionOrNull;

    private bool mIsPersonalScheduleEditorVisible;

    private bool mWasInspectorPaneOpenBeforePersonalScheduleEditing;

    private string mPersonalScheduleTitleDraft;

    private string mPersonalScheduleSectionDraft;

    private string mPersonalScheduleInstructorDraft;

    private string mPersonalScheduleLocationDraft;

    private ScheduleTime? mPersonalScheduleStartTimeOrNull;

    private ScheduleTime? mPersonalScheduleEndTimeOrNull;

    private EPersonalScheduleDraftValidationError mPersonalScheduleValidationError;

    public bool IsPersonalScheduleEditorVisible
    {
        get
        {
            return mIsPersonalScheduleEditorVisible;
        }
    }

    public bool IsDeletePersonalScheduleConfirmationVisible
    {
        get
        {
            return mPersonalSchedulePendingDeletionOrNull != null;
        }
    }

    public bool IsPersonalScheduleOverlayVisible
    {
        get
        {
            return IsPersonalScheduleEditorVisible
                || IsDeletePersonalScheduleConfirmationVisible;
        }
    }

    public string PersonalScheduleEditorHeading
    {
        get
        {
            return mEditingPersonalScheduleIdOrNull.HasValue
                ? "개인 일정 수정"
                : "개인 일정 추가";
        }
    }

    public string PersonalScheduleSaveButtonText
    {
        get
        {
            return mEditingPersonalScheduleIdOrNull.HasValue
                ? "변경 저장"
                : "일정 추가";
        }
    }

    [AllowNull]
    public string PersonalScheduleTitleDraft
    {
        get
        {
            return mPersonalScheduleTitleDraft;
        }
        set
        {
            setDraftString(
                ref mPersonalScheduleTitleDraft,
                value,
                nameof(PersonalScheduleTitleDraft));
        }
    }

    [AllowNull]
    public string PersonalScheduleSectionDraft
    {
        get
        {
            return mPersonalScheduleSectionDraft;
        }
        set
        {
            setDraftString(
                ref mPersonalScheduleSectionDraft,
                value,
                nameof(PersonalScheduleSectionDraft));
        }
    }

    [AllowNull]
    public string PersonalScheduleInstructorDraft
    {
        get
        {
            return mPersonalScheduleInstructorDraft;
        }
        set
        {
            setDraftString(
                ref mPersonalScheduleInstructorDraft,
                value,
                nameof(PersonalScheduleInstructorDraft));
        }
    }

    [AllowNull]
    public string PersonalScheduleLocationDraft
    {
        get
        {
            return mPersonalScheduleLocationDraft;
        }
        set
        {
            setDraftString(
                ref mPersonalScheduleLocationDraft,
                value,
                nameof(PersonalScheduleLocationDraft));
        }
    }

    public IReadOnlyList<PersonalScheduleDayOption> PersonalScheduleDayOptions
    {
        get
        {
            return mPersonalScheduleDayOptions;
        }
    }

    public ScheduleTime? PersonalScheduleStartTimeOrNull
    {
        get
        {
            return mPersonalScheduleStartTimeOrNull;
        }
        set
        {
            if (setProperty(ref mPersonalScheduleStartTimeOrNull, value))
            {
                clearPersonalScheduleValidationError();
            }
        }
    }

    public ScheduleTime? PersonalScheduleEndTimeOrNull
    {
        get
        {
            return mPersonalScheduleEndTimeOrNull;
        }
        set
        {
            if (setProperty(ref mPersonalScheduleEndTimeOrNull, value))
            {
                clearPersonalScheduleValidationError();
            }
        }
    }

    public string PersonalScheduleValidationMessage
    {
        get
        {
            return getPersonalScheduleValidationMessage(
                mPersonalScheduleValidationError);
        }
    }

    public EPersonalScheduleDraftValidationError PersonalScheduleValidationError
    {
        get
        {
            return mPersonalScheduleValidationError;
        }
    }

    public bool HasPersonalScheduleValidationMessage
    {
        get
        {
            return PersonalScheduleValidationError
                != EPersonalScheduleDraftValidationError.None;
        }
    }

    public string PersonalScheduleDeletionDescription
    {
        get
        {
            if (mPersonalSchedulePendingDeletionOrNull == null)
            {
                return string.Empty;
            }

            return "‘" + mPersonalSchedulePendingDeletionOrNull.Title
                + "’ 일정을 이 계획에서 삭제합니다.";
        }
    }

    public ICommand BeginAddPersonalScheduleCommand { get; }

    public ICommand BeginEditPersonalScheduleCommand { get; }

    public ICommand SavePersonalScheduleCommand { get; }

    public ICommand CancelPersonalScheduleEditCommand { get; }

    public ICommand BeginDeletePersonalScheduleCommand { get; }

    public ICommand ConfirmDeletePersonalScheduleCommand { get; }

    public ICommand CancelDeletePersonalScheduleCommand { get; }

    private void beginAddPersonalSchedule()
    {
        throwIfDisposed();
        rememberInspectorPaneStateBeforePersonalScheduleEditing();
        closeCourseChoiceEditingState();
        closePlanEditingState();
        clearPersonalScheduleDraft();
        mEditingPersonalScheduleIdOrNull = null;
        mIsPersonalScheduleEditorVisible = true;
        raisePersonalScheduleOverlayStateChanged();
    }

    private void beginEditPersonalSchedule(PersonalScheduleId scheduleId)
    {
        throwIfDisposed();
        PersonalSchedule schedule = findPersonalSchedule(scheduleId);
        rememberInspectorPaneStateBeforePersonalScheduleEditing();
        closeCourseChoiceEditingState();
        closePlanEditingState();
        clearPersonalScheduleDraft();
        mEditingPersonalScheduleIdOrNull = schedule.Id;
        mPersonalScheduleTitleDraft = schedule.Title.Value;
        setSelectedDays(schedule.TimeRanges);
        DailyTimeRange timeRange = schedule.TimeRanges[0].TimeRange;
        mPersonalScheduleStartTimeOrNull = timeRange.Start;
        mPersonalScheduleEndTimeOrNull = timeRange.End;
        mPersonalScheduleSectionDraft = getSectionValue(schedule.Details);
        mPersonalScheduleInstructorDraft = getInstructorValue(schedule.Details);
        mPersonalScheduleLocationDraft = getLocationValue(schedule.Details);
        mIsPersonalScheduleEditorVisible = true;
        raisePersonalScheduleDraftChanged();
        raisePersonalScheduleOverlayStateChanged();
    }

    private void savePersonalSchedule()
    {
        throwIfDisposed();
        EPersonalScheduleDraftValidationError validationError =
            validatePersonalScheduleDraft();
        if (validationError != EPersonalScheduleDraftValidationError.None)
        {
            showPersonalScheduleValidationError(validationError);
            return;
        }

        PersonalSchedule personalSchedule = createPersonalScheduleFromDraft();
        if (hasPersonalScheduleOverlap(personalSchedule))
        {
            showPersonalScheduleValidationError(
                EPersonalScheduleDraftValidationError.Overlap);
            return;
        }

        if (mEditingPersonalScheduleIdOrNull.HasValue)
        {
            mSession.UpdatePersonalSchedule(personalSchedule);
        }
        else
        {
            mSession.AddPersonalSchedule(personalSchedule);
        }

        closePersonalScheduleEditor();
        afterPlanContentMutation();
    }

    private void cancelPersonalScheduleEdit()
    {
        closePersonalScheduleEditor();
    }

    private void beginDeletePersonalSchedule(PersonalScheduleItem scheduleItem)
    {
        throwIfDisposed();
        if (scheduleItem == null)
        {
            throw new ArgumentNullException(nameof(scheduleItem));
        }

        findPersonalSchedule(scheduleItem.Id);
        closePersonalScheduleEditor();
        mPersonalSchedulePendingDeletionOrNull = scheduleItem;
        raisePersonalScheduleOverlayStateChanged();
    }

    private void confirmDeletePersonalSchedule()
    {
        throwIfDisposed();
        PersonalScheduleItem? scheduleItemOrNull =
            mPersonalSchedulePendingDeletionOrNull;
        if (scheduleItemOrNull == null)
        {
            return;
        }

        mSession.RemovePersonalSchedule(scheduleItemOrNull.Id);
        mPersonalSchedulePendingDeletionOrNull = null;
        raisePersonalScheduleOverlayStateChanged();
        afterPlanContentMutation();
    }

    private void cancelDeletePersonalSchedule()
    {
        if (mPersonalSchedulePendingDeletionOrNull == null)
        {
            return;
        }

        mPersonalSchedulePendingDeletionOrNull = null;
        raisePersonalScheduleOverlayStateChanged();
    }

    private PersonalSchedule findPersonalSchedule(PersonalScheduleId scheduleId)
    {
        foreach (PersonalSchedule schedule in ActivePlan.Plan.PersonalSchedules)
        {
            if (schedule.Id == scheduleId)
            {
                return schedule;
            }
        }

        throw new KeyNotFoundException(
            "The active plan does not contain the personal schedule.");
    }

    private void closePersonalScheduleEditor()
    {
        if (mIsPersonalScheduleEditorVisible == false)
        {
            return;
        }

        mIsPersonalScheduleEditorVisible = false;
        clearPersonalScheduleDraft();
        restoreInspectorPaneStateAfterPersonalScheduleEditing();
        raisePersonalScheduleOverlayStateChanged();
    }

    private void closePersonalScheduleEditingState()
    {
        bool hadVisibleState = IsPersonalScheduleOverlayVisible;
        bool wasEditorVisible = IsPersonalScheduleEditorVisible;
        mIsPersonalScheduleEditorVisible = false;
        mPersonalSchedulePendingDeletionOrNull = null;
        clearPersonalScheduleDraft();
        if (wasEditorVisible)
        {
            restoreInspectorPaneStateAfterPersonalScheduleEditing();
        }

        if (hadVisibleState)
        {
            raisePersonalScheduleOverlayStateChanged();
        }
    }

    private void raisePersonalScheduleOverlayStateChanged()
    {
        raisePropertyChanged(nameof(IsPersonalScheduleEditorVisible));
        raisePropertyChanged(nameof(IsDeletePersonalScheduleConfirmationVisible));
        raisePropertyChanged(nameof(IsPersonalScheduleOverlayVisible));
        raisePropertyChanged(nameof(IsWorkspaceInteractionEnabled));
        raisePropertyChanged(nameof(PersonalScheduleDeletionDescription));
    }

    private void rememberInspectorPaneStateBeforePersonalScheduleEditing()
    {
        mWasInspectorPaneOpenBeforePersonalScheduleEditing =
            IsInspectorPaneOpen;
    }

    private void restoreInspectorPaneStateAfterPersonalScheduleEditing()
    {
        IsInspectorPaneOpen =
            mWasInspectorPaneOpenBeforePersonalScheduleEditing;
    }

}
