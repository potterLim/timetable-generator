using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

public sealed class ScheduleRecommendation
{
    private readonly IReadOnlyList<ScheduledOffering> mScheduledOfferings;

    private readonly IReadOnlyList<UnscheduledOfferingSelection> mUnscheduledSelections;

    private readonly IReadOnlyList<PersonalSchedule> mPersonalSchedules;

    public IReadOnlyList<ScheduledOffering> ScheduledOfferings
    {
        get
        {
            return mScheduledOfferings;
        }
    }

    public IReadOnlyList<UnscheduledOfferingSelection> UnscheduledSelections
    {
        get
        {
            return mUnscheduledSelections;
        }
    }

    public IReadOnlyList<PersonalSchedule> PersonalSchedules
    {
        get
        {
            return mPersonalSchedules;
        }
    }

    public ERecommendationVerificationStatus VerificationStatus { get; }

    public RecommendationScore Score { get; }

    internal ScheduleRecommendation(
        IEnumerable<ScheduledOffering> scheduledOfferings,
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections,
        IEnumerable<PersonalSchedule> personalSchedules,
        RecommendationScore score)
    {
        if (scheduledOfferings == null)
        {
            throw new ArgumentNullException(nameof(scheduledOfferings));
        }

        if (unscheduledSelections == null)
        {
            throw new ArgumentNullException(nameof(unscheduledSelections));
        }

        if (personalSchedules == null)
        {
            throw new ArgumentNullException(nameof(personalSchedules));
        }

        if (score.IsValid == false)
        {
            throw new ArgumentException("Schedule recommendations require a valid score.", nameof(score));
        }

        IReadOnlyList<ScheduledOffering> copiedScheduledOfferings = copyAndValidateScheduledOfferings(scheduledOfferings);
        IReadOnlyList<UnscheduledOfferingSelection> copiedUnscheduledSelections = copyUnscheduledSelections(unscheduledSelections);
        IReadOnlyList<PersonalSchedule> copiedPersonalSchedules = copyPersonalSchedules(personalSchedules);
        validateUniqueCourseAndOfferingSelections(copiedScheduledOfferings, copiedUnscheduledSelections, nameof(scheduledOfferings), nameof(unscheduledSelections));
        validateFixedScheduleConflicts(copiedScheduledOfferings, copiedPersonalSchedules);
        if (copiedScheduledOfferings.Count == 0
            && copiedUnscheduledSelections.Count == 0
            && copiedPersonalSchedules.Count == 0)
        {
            throw new ArgumentException("Schedule recommendations require at least one selected item.");
        }

        mScheduledOfferings = copiedScheduledOfferings;
        mUnscheduledSelections = copiedUnscheduledSelections;
        mPersonalSchedules = copiedPersonalSchedules;
        Score = score;
        VerificationStatus = copiedUnscheduledSelections.Count == 0
            ? ERecommendationVerificationStatus.ConfirmedConflictFree
            : ERecommendationVerificationStatus.RequiresManualReview;
    }

    private static IReadOnlyList<ScheduledOffering> copyAndValidateScheduledOfferings(
        IEnumerable<ScheduledOffering> scheduledOfferings)
    {
        List<ScheduledOffering> copiedOfferings = new List<ScheduledOffering>();
        foreach (ScheduledOffering scheduledOffering in scheduledOfferings)
        {
            if (scheduledOffering == null)
            {
                throw new ArgumentException(
                    "Schedule recommendations cannot contain null offerings.",
                    nameof(scheduledOfferings));
            }

            foreach (ScheduledOffering selectedOffering in copiedOfferings)
            {
                if (ScheduleConflictDetector.HasConflict(selectedOffering, scheduledOffering))
                {
                    throw new ArgumentException(
                        "Schedule recommendations cannot contain conflicting offerings.",
                        nameof(scheduledOfferings));
                }
            }

            copiedOfferings.Add(scheduledOffering);
        }

        return copiedOfferings.AsReadOnly();
    }

    private static IReadOnlyList<UnscheduledOfferingSelection> copyUnscheduledSelections(
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections)
    {
        List<UnscheduledOfferingSelection> copiedSelections = new List<UnscheduledOfferingSelection>();
        foreach (UnscheduledOfferingSelection selection in unscheduledSelections)
        {
            if (selection == null)
            {
                throw new ArgumentException(
                    "Schedule recommendations cannot contain null unscheduled selections.",
                    nameof(unscheduledSelections));
            }

            copiedSelections.Add(selection);
        }

        return copiedSelections.AsReadOnly();
    }

    private static void validateUniqueCourseAndOfferingSelections(
        IReadOnlyList<ScheduledOffering> scheduledOfferings,
        IReadOnlyList<UnscheduledOfferingSelection> unscheduledSelections,
        string scheduledParameterName,
        string unscheduledParameterName)
    {
        HashSet<CourseId> selectedCourseIds = new HashSet<CourseId>();
        HashSet<OfferingId> selectedOfferingIds = new HashSet<OfferingId>();
        foreach (ScheduledOffering offering in scheduledOfferings)
        {
            addUniqueSelection(
                offering.CourseId,
                offering.OfferingId,
                selectedCourseIds,
                selectedOfferingIds,
                scheduledParameterName);
        }

        foreach (UnscheduledOfferingSelection selection in unscheduledSelections)
        {
            addUniqueSelection(
                selection.CourseId,
                selection.OfferingId,
                selectedCourseIds,
                selectedOfferingIds,
                unscheduledParameterName);
        }
    }

    private static void addUniqueSelection(
        CourseId courseId,
        OfferingId offeringId,
        ISet<CourseId> selectedCourseIds,
        ISet<OfferingId> selectedOfferingIds,
        string parameterName)
    {
        if (selectedCourseIds.Add(courseId) == false)
        {
            throw new ArgumentException(
                "Schedule recommendations can select only one offering per course.",
                parameterName);
        }

        if (selectedOfferingIds.Add(offeringId) == false)
        {
            throw new ArgumentException(
                "Schedule recommendations cannot contain duplicate offerings.",
                parameterName);
        }
    }

    private static IReadOnlyList<PersonalSchedule> copyPersonalSchedules(
        IEnumerable<PersonalSchedule> personalSchedules)
    {
        List<PersonalSchedule> copiedSchedules = new List<PersonalSchedule>();
        HashSet<PersonalScheduleId> scheduleIds = new HashSet<PersonalScheduleId>();
        foreach (PersonalSchedule personalSchedule in personalSchedules)
        {
            if (personalSchedule == null)
            {
                throw new ArgumentException(
                    "Schedule recommendations cannot contain null personal schedules.",
                    nameof(personalSchedules));
            }

            if (scheduleIds.Add(personalSchedule.Id) == false)
            {
                throw new ArgumentException(
                    "Schedule recommendations cannot contain duplicate personal schedules.",
                    nameof(personalSchedules));
            }

            copiedSchedules.Add(personalSchedule);
        }

        return copiedSchedules.AsReadOnly();
    }

    private static void validateFixedScheduleConflicts(
        IEnumerable<ScheduledOffering> scheduledOfferings,
        IEnumerable<PersonalSchedule> personalSchedules)
    {
        foreach (ScheduledOffering scheduledOffering in scheduledOfferings)
        {
            foreach (MeetingSlot meetingSlot in scheduledOffering.MeetingSlots)
            {
                WeeklyTimeRange offeringTimeRange = AcademicPeriodTimeTable.GetWeeklyTimeRange(meetingSlot);
                foreach (PersonalSchedule personalSchedule in personalSchedules)
                {
                    foreach (WeeklyTimeRange personalTimeRange
                        in personalSchedule.TimeRanges)
                    {
                        if (ScheduleConflictDetector.HasConflict(offeringTimeRange, personalTimeRange))
                        {
                            throw new ArgumentException(
                                "Schedule recommendations cannot contain a course "
                                + "that conflicts with a personal schedule.",
                                nameof(scheduledOfferings));
                        }
                    }
                }
            }
        }
    }
}
