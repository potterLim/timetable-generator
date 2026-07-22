using System;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.ViewModels;

internal sealed partial class PlannerWorkspaceViewModel
{
    private bool hasPersonalScheduleOverlap(PersonalSchedule candidate)
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
                    if (ScheduleConflictDetector.HasConflict(existingRange, candidateRange))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private void showPersonalScheduleValidationError(
        EPersonalScheduleDraftValidationError validationError)
    {
        if (validationError == EPersonalScheduleDraftValidationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(validationError));
        }

        mPersonalScheduleValidationError = validationError;
        raisePropertyChanged(nameof(PersonalScheduleValidationMessage));
        raisePropertyChanged(nameof(PersonalScheduleValidationError));
        raisePropertyChanged(nameof(HasPersonalScheduleValidationMessage));
    }

    private static string getPersonalScheduleValidationMessage(
        EPersonalScheduleDraftValidationError validationError)
    {
        switch (validationError)
        {
            case EPersonalScheduleDraftValidationError.None:
                return string.Empty;
            case EPersonalScheduleDraftValidationError.TitleRequired:
                return "이름을 입력해 주세요.";
            case EPersonalScheduleDraftValidationError.TitleInvalid:
                return "이름은 줄바꿈 없이 "
                    + PersonalScheduleTitle.MAXIMUM_LENGTH
                    + "자 이내로 입력해 주세요.";
            case EPersonalScheduleDraftValidationError.DayRequired:
                return "적용할 요일을 하나 이상 선택해 주세요.";
            case EPersonalScheduleDraftValidationError.StartTimeRequired:
                return "시작 시간을 입력해 주세요.";
            case EPersonalScheduleDraftValidationError.EndTimeRequired:
                return "종료 시간을 입력해 주세요.";
            case EPersonalScheduleDraftValidationError.StartTimePrecisionInvalid:
            case EPersonalScheduleDraftValidationError.EndTimePrecisionInvalid:
                return "시간은 "
                    + PersonalSchedule.TIME_INCREMENT_MINUTES
                    + "분 단위로 입력해 주세요.";
            case EPersonalScheduleDraftValidationError.EndNotAfterStart:
                return "종료 시간은 시작 시간보다 늦어야 합니다.";
            case EPersonalScheduleDraftValidationError.DurationTooShort:
                return "개인 일정은 "
                    + PersonalSchedule.MINIMUM_DURATION_MINUTES
                    + "분 이상으로 입력해 주세요.";
            case EPersonalScheduleDraftValidationError.SectionInvalid:
                return "분반은 줄바꿈 없이 "
                    + PersonalScheduleSection.MAXIMUM_LENGTH
                    + "자 이내로 입력해 주세요.";
            case EPersonalScheduleDraftValidationError.InstructorInvalid:
                return "교수·담당자는 줄바꿈 없이 "
                    + PersonalScheduleInstructor.MAXIMUM_LENGTH
                    + "자 이내로 입력해 주세요.";
            case EPersonalScheduleDraftValidationError.LocationInvalid:
                return "장소는 줄바꿈 없이 "
                    + PersonalScheduleLocation.MAXIMUM_LENGTH
                    + "자 이내로 입력해 주세요.";
            case EPersonalScheduleDraftValidationError.Overlap:
                return "같은 요일과 시간에 다른 개인 일정이 있습니다.";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(validationError),
                    validationError,
                    "Unknown personal schedule validation error.");
        }
    }

    private EPersonalScheduleDraftValidationError validatePersonalScheduleDraft()
    {
        string normalizedTitle = PersonalScheduleTitleDraft.Trim();
        if (normalizedTitle.Length == 0)
        {
            return EPersonalScheduleDraftValidationError.TitleRequired;
        }

        if (hasInvalidPersonalScheduleText(normalizedTitle, PersonalScheduleTitle.MAXIMUM_LENGTH))
        {
            return EPersonalScheduleDraftValidationError.TitleInvalid;
        }

        if (hasSelectedPersonalScheduleDay() == false)
        {
            return EPersonalScheduleDraftValidationError.DayRequired;
        }

        if (PersonalScheduleStartTimeOrNull.HasValue == false)
        {
            return EPersonalScheduleDraftValidationError.StartTimeRequired;
        }

        if (PersonalScheduleEndTimeOrNull.HasValue == false)
        {
            return EPersonalScheduleDraftValidationError.EndTimeRequired;
        }

        ScheduleTime startTime = PersonalScheduleStartTimeOrNull.Value;
        ScheduleTime endTime = PersonalScheduleEndTimeOrNull.Value;
        if (hasSupportedTimePrecision(startTime) == false)
        {
            return EPersonalScheduleDraftValidationError.StartTimePrecisionInvalid;
        }

        if (hasSupportedTimePrecision(endTime) == false)
        {
            return EPersonalScheduleDraftValidationError.EndTimePrecisionInvalid;
        }

        if (endTime.CompareTo(startTime) <= 0)
        {
            return EPersonalScheduleDraftValidationError.EndNotAfterStart;
        }

        int durationMinutes = endTime.MinutesFromMidnight - startTime.MinutesFromMidnight;
        if (durationMinutes < PersonalSchedule.MINIMUM_DURATION_MINUTES)
        {
            return EPersonalScheduleDraftValidationError.DurationTooShort;
        }

        if (hasInvalidOptionalPersonalScheduleText(
            PersonalScheduleSectionDraft,
            PersonalScheduleSection.MAXIMUM_LENGTH))
        {
            return EPersonalScheduleDraftValidationError.SectionInvalid;
        }

        if (hasInvalidOptionalPersonalScheduleText(
            PersonalScheduleInstructorDraft,
            PersonalScheduleInstructor.MAXIMUM_LENGTH))
        {
            return EPersonalScheduleDraftValidationError.InstructorInvalid;
        }

        if (hasInvalidOptionalPersonalScheduleText(
            PersonalScheduleLocationDraft,
            PersonalScheduleLocation.MAXIMUM_LENGTH))
        {
            return EPersonalScheduleDraftValidationError.LocationInvalid;
        }

        return EPersonalScheduleDraftValidationError.None;
    }

    private static bool hasSupportedTimePrecision(ScheduleTime value)
    {
        bool usesSupportedIncrement = value.Minute
            % PersonalSchedule.TIME_INCREMENT_MINUTES == 0;
        return value.IsValid && usesSupportedIncrement;
    }

    private static bool hasInvalidOptionalPersonalScheduleText(string value, int maximumLength)
    {
        string normalizedValue = value.Trim();
        return normalizedValue.Length > 0
            && hasInvalidPersonalScheduleText(normalizedValue, maximumLength);
    }

    private static bool hasInvalidPersonalScheduleText(string value, int maximumLength)
    {
        return value.Length > maximumLength
            || value.Contains('\r')
            || value.Contains('\n');
    }
}
