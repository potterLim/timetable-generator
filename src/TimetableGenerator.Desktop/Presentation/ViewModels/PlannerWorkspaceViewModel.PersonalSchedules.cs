using System;
using System.Collections.Generic;
using System.Windows.Input;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private PersonalScheduleId? mEditingPersonalScheduleIdOrNull;

    private PersonalScheduleItem? mPersonalSchedulePendingDeletionOrNull;

    private bool mIsPersonalScheduleEditorVisible;

    private string mPersonalScheduleTitleDraft;

    private string mPersonalScheduleSectionDraft;

    private string mPersonalScheduleInstructorDraft;

    private string mPersonalScheduleLocationDraft;

    private bool mIsMondaySelected;

    private bool mIsTuesdaySelected;

    private bool mIsWednesdaySelected;

    private bool mIsThursdaySelected;

    private bool mIsFridaySelected;

    private TimeSpan mPersonalScheduleStartTime;

    private TimeSpan mPersonalScheduleEndTime;

    private string mPersonalScheduleValidationMessage;

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

    public string PersonalScheduleEditorDescription
    {
        get
        {
            return "‘" + ActivePlan.DisplayName
                + "’ 계획의 모든 추천 시간표에만 적용됩니다.";
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

    public bool IsMondaySelected
    {
        get
        {
            return mIsMondaySelected;
        }
        set
        {
            setDaySelection(
                ref mIsMondaySelected,
                value,
                nameof(IsMondaySelected));
        }
    }

    public bool IsTuesdaySelected
    {
        get
        {
            return mIsTuesdaySelected;
        }
        set
        {
            setDaySelection(
                ref mIsTuesdaySelected,
                value,
                nameof(IsTuesdaySelected));
        }
    }

    public bool IsWednesdaySelected
    {
        get
        {
            return mIsWednesdaySelected;
        }
        set
        {
            setDaySelection(
                ref mIsWednesdaySelected,
                value,
                nameof(IsWednesdaySelected));
        }
    }

    public bool IsThursdaySelected
    {
        get
        {
            return mIsThursdaySelected;
        }
        set
        {
            setDaySelection(
                ref mIsThursdaySelected,
                value,
                nameof(IsThursdaySelected));
        }
    }

    public bool IsFridaySelected
    {
        get
        {
            return mIsFridaySelected;
        }
        set
        {
            setDaySelection(
                ref mIsFridaySelected,
                value,
                nameof(IsFridaySelected));
        }
    }

    public TimeSpan PersonalScheduleStartTime
    {
        get
        {
            return mPersonalScheduleStartTime;
        }
        set
        {
            if (setProperty(ref mPersonalScheduleStartTime, value))
            {
                clearPersonalScheduleValidationMessage();
            }
        }
    }

    public TimeSpan PersonalScheduleEndTime
    {
        get
        {
            return mPersonalScheduleEndTime;
        }
        set
        {
            if (setProperty(ref mPersonalScheduleEndTime, value))
            {
                clearPersonalScheduleValidationMessage();
            }
        }
    }

    public string PersonalScheduleValidationMessage
    {
        get
        {
            return mPersonalScheduleValidationMessage;
        }
    }

    public bool HasPersonalScheduleValidationMessage
    {
        get
        {
            return PersonalScheduleValidationMessage.Length > 0;
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
                + "’ 일정을 이 계획의 추천 시간표와 PNG에서 제거합니다.";
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
        closePlanEditingState();
        clearPersonalScheduleDraft();
        mEditingPersonalScheduleIdOrNull = null;
        mIsPersonalScheduleEditorVisible = true;
        raisePersonalScheduleOverlayStateChanged();
    }

    private void beginEditPersonalSchedule(PersonalScheduleItem scheduleItem)
    {
        throwIfDisposed();
        if (scheduleItem == null)
        {
            throw new ArgumentNullException(nameof(scheduleItem));
        }

        PersonalSchedule schedule = findPersonalSchedule(scheduleItem.Id);
        closePlanEditingState();
        clearPersonalScheduleDraft();
        mEditingPersonalScheduleIdOrNull = schedule.Id;
        mPersonalScheduleTitleDraft = schedule.Title.Value;
        setSelectedDays(schedule.TimeRanges);
        DailyTimeRange timeRange = schedule.TimeRanges[0].TimeRange;
        mPersonalScheduleStartTime = createTimeSpan(timeRange.Start);
        mPersonalScheduleEndTime = createTimeSpan(timeRange.End);
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
        try
        {
            PersonalSchedule personalSchedule = createPersonalScheduleFromDraft();
            ensurePersonalScheduleDoesNotOverlap(personalSchedule);
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
        catch (ArgumentException exception)
        {
            mPersonalScheduleValidationMessage = getValidationMessage(exception);
            raisePropertyChanged(nameof(PersonalScheduleValidationMessage));
            raisePropertyChanged(nameof(HasPersonalScheduleValidationMessage));
        }
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

    private PersonalSchedule createPersonalScheduleFromDraft()
    {
        PersonalScheduleId scheduleId = mEditingPersonalScheduleIdOrNull.HasValue
            ? mEditingPersonalScheduleIdOrNull.Value
            : PersonalScheduleId.CreateNew();
        PersonalScheduleTitle title = new PersonalScheduleTitle(
            PersonalScheduleTitleDraft);
        ScheduleTime start = createScheduleTime(PersonalScheduleStartTime);
        ScheduleTime end = createScheduleTime(PersonalScheduleEndTime);
        DailyTimeRange timeRange = new DailyTimeRange(start, end);
        if (timeRange.DurationMinutes < 15)
        {
            throw new ArgumentException("Personal schedules require 15 minutes.");
        }

        IReadOnlyList<WeeklyTimeRange> timeRanges = createSelectedTimeRanges(timeRange);
        PersonalScheduleDetails details = new PersonalScheduleDetails(
            createSectionOrNull(PersonalScheduleSectionDraft),
            createInstructorOrNull(PersonalScheduleInstructorDraft),
            createLocationOrNull(PersonalScheduleLocationDraft));
        return new PersonalSchedule(scheduleId, title, timeRanges, details);
    }

    private IReadOnlyList<WeeklyTimeRange> createSelectedTimeRanges(
        DailyTimeRange timeRange)
    {
        List<WeeklyTimeRange> timeRanges = new List<WeeklyTimeRange>();
        addSelectedTimeRange(timeRanges, IsMondaySelected, EDay.Monday, timeRange);
        addSelectedTimeRange(timeRanges, IsTuesdaySelected, EDay.Tuesday, timeRange);
        addSelectedTimeRange(timeRanges, IsWednesdaySelected, EDay.Wednesday, timeRange);
        addSelectedTimeRange(timeRanges, IsThursdaySelected, EDay.Thursday, timeRange);
        addSelectedTimeRange(timeRanges, IsFridaySelected, EDay.Friday, timeRange);
        if (timeRanges.Count == 0)
        {
            throw new ArgumentException("A weekday must be selected.");
        }

        return timeRanges.AsReadOnly();
    }

    private static void addSelectedTimeRange(
        ICollection<WeeklyTimeRange> timeRanges,
        bool isSelected,
        EDay day,
        DailyTimeRange timeRange)
    {
        if (isSelected)
        {
            timeRanges.Add(new WeeklyTimeRange(day, timeRange));
        }
    }

    private void ensurePersonalScheduleDoesNotOverlap(PersonalSchedule candidate)
    {
        foreach (PersonalSchedule existing in ActivePlan.Plan.PersonalSchedules)
        {
            if (existing.Id == candidate.Id)
            {
                continue;
            }

            foreach (WeeklyTimeRange existingRange in existing.TimeRanges)
            {
                foreach (WeeklyTimeRange candidateRange in candidate.TimeRanges)
                {
                    if (ScheduleConflictDetector.HasConflict(
                        existingRange,
                        candidateRange))
                    {
                        throw new ArgumentException("Personal schedules overlap.");
                    }
                }
            }
        }
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
        raisePersonalScheduleOverlayStateChanged();
    }

    private void closePersonalScheduleEditingState()
    {
        bool hadVisibleState = IsPersonalScheduleOverlayVisible;
        mIsPersonalScheduleEditorVisible = false;
        mPersonalSchedulePendingDeletionOrNull = null;
        clearPersonalScheduleDraft();
        if (hadVisibleState)
        {
            raisePersonalScheduleOverlayStateChanged();
        }
    }

    private void clearPersonalScheduleDraft()
    {
        mEditingPersonalScheduleIdOrNull = null;
        mPersonalScheduleTitleDraft = string.Empty;
        mPersonalScheduleSectionDraft = string.Empty;
        mPersonalScheduleInstructorDraft = string.Empty;
        mPersonalScheduleLocationDraft = string.Empty;
        mIsMondaySelected = false;
        mIsTuesdaySelected = false;
        mIsWednesdaySelected = false;
        mIsThursdaySelected = false;
        mIsFridaySelected = false;
        mPersonalScheduleStartTime = new TimeSpan(12, 0, 0);
        mPersonalScheduleEndTime = new TimeSpan(13, 0, 0);
        mPersonalScheduleValidationMessage = string.Empty;
        raisePersonalScheduleDraftChanged();
    }

    private void setSelectedDays(IEnumerable<WeeklyTimeRange> timeRanges)
    {
        foreach (WeeklyTimeRange timeRange in timeRanges)
        {
            switch (timeRange.Day)
            {
                case EDay.Monday:
                    mIsMondaySelected = true;
                    break;
                case EDay.Tuesday:
                    mIsTuesdaySelected = true;
                    break;
                case EDay.Wednesday:
                    mIsWednesdaySelected = true;
                    break;
                case EDay.Thursday:
                    mIsThursdaySelected = true;
                    break;
                case EDay.Friday:
                    mIsFridaySelected = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(timeRanges),
                        timeRange.Day,
                        "The personal schedule editor supports weekdays only.");
            }
        }
    }

    private void setDraftString(
        ref string field,
        string value,
        string propertyName)
    {
        string normalizedValue = value;
        if (normalizedValue == null)
        {
            normalizedValue = string.Empty;
        }

        if (setProperty(ref field, normalizedValue, propertyName))
        {
            clearPersonalScheduleValidationMessage();
        }
    }

    private void setDaySelection(
        ref bool field,
        bool value,
        string propertyName)
    {
        if (setProperty(ref field, value, propertyName))
        {
            clearPersonalScheduleValidationMessage();
        }
    }

    private void clearPersonalScheduleValidationMessage()
    {
        if (mPersonalScheduleValidationMessage.Length == 0)
        {
            return;
        }

        mPersonalScheduleValidationMessage = string.Empty;
        raisePropertyChanged(nameof(PersonalScheduleValidationMessage));
        raisePropertyChanged(nameof(HasPersonalScheduleValidationMessage));
    }

    private void raisePersonalScheduleDraftChanged()
    {
        raisePropertyChanged(nameof(PersonalScheduleTitleDraft));
        raisePropertyChanged(nameof(PersonalScheduleSectionDraft));
        raisePropertyChanged(nameof(PersonalScheduleInstructorDraft));
        raisePropertyChanged(nameof(PersonalScheduleLocationDraft));
        raisePropertyChanged(nameof(IsMondaySelected));
        raisePropertyChanged(nameof(IsTuesdaySelected));
        raisePropertyChanged(nameof(IsWednesdaySelected));
        raisePropertyChanged(nameof(IsThursdaySelected));
        raisePropertyChanged(nameof(IsFridaySelected));
        raisePropertyChanged(nameof(PersonalScheduleStartTime));
        raisePropertyChanged(nameof(PersonalScheduleEndTime));
        raisePropertyChanged(nameof(PersonalScheduleValidationMessage));
        raisePropertyChanged(nameof(HasPersonalScheduleValidationMessage));
        raisePropertyChanged(nameof(PersonalScheduleEditorHeading));
        raisePropertyChanged(nameof(PersonalScheduleEditorDescription));
        raisePropertyChanged(nameof(PersonalScheduleSaveButtonText));
    }

    private void raisePersonalScheduleOverlayStateChanged()
    {
        raisePropertyChanged(nameof(IsPersonalScheduleEditorVisible));
        raisePropertyChanged(nameof(IsDeletePersonalScheduleConfirmationVisible));
        raisePropertyChanged(nameof(IsPersonalScheduleOverlayVisible));
        raisePropertyChanged(nameof(IsWorkspaceInteractionEnabled));
        raisePropertyChanged(nameof(PersonalScheduleDeletionDescription));
    }

    private static ScheduleTime createScheduleTime(TimeSpan value)
    {
        if (value.Days != 0 || value.Seconds != 0 || value.Milliseconds != 0)
        {
            throw new ArgumentException("Schedule times must use minute precision.");
        }

        return new ScheduleTime(value.Hours, value.Minutes);
    }

    private static TimeSpan createTimeSpan(ScheduleTime value)
    {
        return new TimeSpan(value.Hour, value.Minute, 0);
    }

    private static PersonalScheduleSection? createSectionOrNull(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new PersonalScheduleSection(value);
    }

    private static PersonalScheduleInstructor? createInstructorOrNull(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new PersonalScheduleInstructor(value);
    }

    private static PersonalScheduleLocation? createLocationOrNull(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new PersonalScheduleLocation(value);
    }

    private static string getSectionValue(PersonalScheduleDetails details)
    {
        return details.SectionOrNull == null
            ? string.Empty
            : details.SectionOrNull.Value;
    }

    private static string getInstructorValue(PersonalScheduleDetails details)
    {
        return details.InstructorOrNull == null
            ? string.Empty
            : details.InstructorOrNull.Value;
    }

    private static string getLocationValue(PersonalScheduleDetails details)
    {
        return details.LocationOrNull == null
            ? string.Empty
            : details.LocationOrNull.Value;
    }

    private static string getValidationMessage(ArgumentException exception)
    {
        if (exception.Message.Contains("overlap", StringComparison.OrdinalIgnoreCase))
        {
            return "같은 요일과 시간에 다른 개인 일정이 있습니다.";
        }

        if (exception.Message.Contains("weekday", StringComparison.OrdinalIgnoreCase))
        {
            return "적용할 요일을 하나 이상 선택해 주세요.";
        }

        if (exception.Message.Contains("end after", StringComparison.OrdinalIgnoreCase))
        {
            return "종료 시간은 시작 시간보다 늦어야 합니다.";
        }

        if (exception.Message.Contains("15 minutes", StringComparison.OrdinalIgnoreCase))
        {
            return "개인 일정은 15분 이상으로 입력해 주세요.";
        }

        return "일정 이름과 시간을 확인해 주세요. 이름은 1~80자로 입력할 수 있습니다.";
    }
}
