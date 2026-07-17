using System;
using System.Collections.Generic;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private PersonalSchedule createPersonalScheduleFromDraft()
    {
        PersonalScheduleId scheduleId = mEditingPersonalScheduleIdOrNull.HasValue
            ? mEditingPersonalScheduleIdOrNull.Value
            : PersonalScheduleId.CreateNew();
        PersonalScheduleTitle title = new PersonalScheduleTitle(
            PersonalScheduleTitleDraft);
        TimeSpan startTime = getRequiredTime(
            PersonalScheduleStartTimeOrNull,
            nameof(PersonalScheduleStartTimeOrNull));
        TimeSpan endTime = getRequiredTime(
            PersonalScheduleEndTimeOrNull,
            nameof(PersonalScheduleEndTimeOrNull));
        ScheduleTime start = createScheduleTime(startTime);
        ScheduleTime end = createScheduleTime(endTime);
        DailyTimeRange timeRange = new DailyTimeRange(start, end);
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
        if (IsMondaySelected)
        {
            timeRanges.Add(new WeeklyTimeRange(EDay.Monday, timeRange));
        }

        if (IsTuesdaySelected)
        {
            timeRanges.Add(new WeeklyTimeRange(EDay.Tuesday, timeRange));
        }

        if (IsWednesdaySelected)
        {
            timeRanges.Add(new WeeklyTimeRange(EDay.Wednesday, timeRange));
        }

        if (IsThursdaySelected)
        {
            timeRanges.Add(new WeeklyTimeRange(EDay.Thursday, timeRange));
        }

        if (IsFridaySelected)
        {
            timeRanges.Add(new WeeklyTimeRange(EDay.Friday, timeRange));
        }

        return timeRanges.AsReadOnly();
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
        mPersonalScheduleStartTimeOrNull = DEFAULT_PERSONAL_SCHEDULE_START_TIME;
        mPersonalScheduleEndTimeOrNull = DEFAULT_PERSONAL_SCHEDULE_END_TIME;
        mPersonalScheduleValidationError = EPersonalScheduleDraftValidationError.None;
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
        string? valueOrNull,
        string propertyName)
    {
        string normalizedValue;
        if (valueOrNull == null)
        {
            normalizedValue = string.Empty;
        }
        else
        {
            normalizedValue = valueOrNull;
        }

        if (setProperty(ref field, normalizedValue, propertyName))
        {
            clearPersonalScheduleValidationError();
        }
    }

    private void setDaySelection(
        ref bool field,
        bool value,
        string propertyName)
    {
        if (setProperty(ref field, value, propertyName))
        {
            clearPersonalScheduleValidationError();
        }
    }

    private void clearPersonalScheduleValidationError()
    {
        if (mPersonalScheduleValidationError
            == EPersonalScheduleDraftValidationError.None)
        {
            return;
        }

        mPersonalScheduleValidationError = EPersonalScheduleDraftValidationError.None;
        raisePropertyChanged(nameof(PersonalScheduleValidationMessage));
        raisePropertyChanged(nameof(PersonalScheduleValidationError));
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
        raisePropertyChanged(nameof(PersonalScheduleStartTimeOrNull));
        raisePropertyChanged(nameof(PersonalScheduleEndTimeOrNull));
        raisePropertyChanged(nameof(PersonalScheduleValidationMessage));
        raisePropertyChanged(nameof(PersonalScheduleValidationError));
        raisePropertyChanged(nameof(HasPersonalScheduleValidationMessage));
        raisePropertyChanged(nameof(PersonalScheduleEditorHeading));
        raisePropertyChanged(nameof(PersonalScheduleEditorDescription));
        raisePropertyChanged(nameof(PersonalScheduleSaveButtonText));
    }

    private static ScheduleTime createScheduleTime(TimeSpan value)
    {
        if (value.Days != 0 || value.Seconds != 0 || value.Milliseconds != 0)
        {
            throw new ArgumentException("Schedule times must use minute precision.");
        }

        return new ScheduleTime(value.Hours, value.Minutes);
    }

    private static TimeSpan getRequiredTime(
        TimeSpan? valueOrNull,
        string propertyName)
    {
        if (valueOrNull.HasValue == false)
        {
            throw new InvalidOperationException(
                propertyName + " must be validated before creating a schedule.");
        }

        return valueOrNull.Value;
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
}
