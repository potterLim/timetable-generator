using System;
using System.Collections.Generic;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private static readonly IReadOnlyList<EDay> PERSONAL_SCHEDULE_DAYS =
        Array.AsReadOnly(
            new EDay[]
            {
                EDay.Monday,
                EDay.Tuesday,
                EDay.Wednesday,
                EDay.Thursday,
                EDay.Friday,
                EDay.Saturday,
                EDay.Sunday,
            });

    private PersonalSchedule createPersonalScheduleFromDraft()
    {
        PersonalScheduleId scheduleId = mEditingPersonalScheduleIdOrNull.HasValue
            ? mEditingPersonalScheduleIdOrNull.Value
            : PersonalScheduleId.CreateNew();
        PersonalScheduleTitle title = new PersonalScheduleTitle(
            PersonalScheduleTitleDraft);
        ScheduleTime startTime = getRequiredTime(
            PersonalScheduleStartTimeOrNull,
            nameof(PersonalScheduleStartTimeOrNull));
        ScheduleTime endTime = getRequiredTime(
            PersonalScheduleEndTimeOrNull,
            nameof(PersonalScheduleEndTimeOrNull));
        DailyTimeRange timeRange = new DailyTimeRange(startTime, endTime);
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
        foreach (PersonalScheduleDayOption dayOption in PersonalScheduleDayOptions)
        {
            if (dayOption.IsSelected)
            {
                timeRanges.Add(new WeeklyTimeRange(dayOption.Day, timeRange));
            }
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
        foreach (PersonalScheduleDayOption dayOption in PersonalScheduleDayOptions)
        {
            dayOption.IsSelected = false;
        }

        mPersonalScheduleStartTimeOrNull = DEFAULT_PERSONAL_SCHEDULE_START_TIME;
        mPersonalScheduleEndTimeOrNull = DEFAULT_PERSONAL_SCHEDULE_END_TIME;
        mPersonalScheduleValidationError = EPersonalScheduleDraftValidationError.None;
        raisePersonalScheduleDraftChanged();
    }

    private void setSelectedDays(IEnumerable<WeeklyTimeRange> timeRanges)
    {
        foreach (WeeklyTimeRange timeRange in timeRanges)
        {
            PersonalScheduleDayOption dayOption =
                findPersonalScheduleDayOption(timeRange.Day);
            dayOption.IsSelected = true;
        }
    }

    private IReadOnlyList<PersonalScheduleDayOption>
        createPersonalScheduleDayOptions()
    {
        List<PersonalScheduleDayOption> dayOptions =
            new List<PersonalScheduleDayOption>();
        foreach (EDay day in PERSONAL_SCHEDULE_DAYS)
        {
            PersonalScheduleDayOption dayOption =
                new PersonalScheduleDayOption(day);
            dayOption.SelectionChanged += onPersonalScheduleDaySelectionChanged;
            dayOptions.Add(dayOption);
        }

        return dayOptions.AsReadOnly();
    }

    private PersonalScheduleDayOption findPersonalScheduleDayOption(EDay day)
    {
        foreach (PersonalScheduleDayOption dayOption in PersonalScheduleDayOptions)
        {
            if (dayOption.Day == day)
            {
                return dayOption;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(day),
            day,
            "The personal schedule editor requires a day from Monday through Sunday.");
    }

    private bool hasSelectedPersonalScheduleDay()
    {
        foreach (PersonalScheduleDayOption dayOption in PersonalScheduleDayOptions)
        {
            if (dayOption.IsSelected)
            {
                return true;
            }
        }

        return false;
    }

    private void onPersonalScheduleDaySelectionChanged(
        object? senderOrNull,
        EventArgs eventArguments)
    {
        clearPersonalScheduleValidationError();
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
        raisePropertyChanged(nameof(PersonalScheduleDayOptions));
        raisePropertyChanged(nameof(PersonalScheduleStartTimeOrNull));
        raisePropertyChanged(nameof(PersonalScheduleEndTimeOrNull));
        raisePropertyChanged(nameof(PersonalScheduleValidationMessage));
        raisePropertyChanged(nameof(PersonalScheduleValidationError));
        raisePropertyChanged(nameof(HasPersonalScheduleValidationMessage));
        raisePropertyChanged(nameof(PersonalScheduleEditorHeading));
        raisePropertyChanged(nameof(PersonalScheduleEditorDescription));
        raisePropertyChanged(nameof(PersonalScheduleSaveButtonText));
    }

    private static ScheduleTime getRequiredTime(
        ScheduleTime? valueOrNull,
        string propertyName)
    {
        if (valueOrNull.HasValue == false)
        {
            throw new InvalidOperationException(
                propertyName + " must be validated before creating a schedule.");
        }

        return valueOrNull.Value;
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
