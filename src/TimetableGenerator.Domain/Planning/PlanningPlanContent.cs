using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Domain.Planning;

public sealed class PlanningPlanContent
{
    private readonly IReadOnlyList<ScheduledCourseChoice> mScheduledCourseChoices;

    private readonly IReadOnlyList<UnscheduledOfferingSelection>
        mUnscheduledOfferingSelections;

    private readonly IReadOnlyList<PersonalSchedule> mPersonalSchedules;

    public IReadOnlyList<ScheduledCourseChoice> ScheduledCourseChoices
    {
        get
        {
            return mScheduledCourseChoices;
        }
    }

    public IReadOnlyList<UnscheduledOfferingSelection> UnscheduledOfferingSelections
    {
        get
        {
            return mUnscheduledOfferingSelections;
        }
    }

    public IReadOnlyList<PersonalSchedule> PersonalSchedules
    {
        get
        {
            return mPersonalSchedules;
        }
    }

    public PlanningPlanContent(
        IEnumerable<ScheduledCourseChoice> scheduledCourseChoices,
        IEnumerable<UnscheduledOfferingSelection> unscheduledOfferingSelections,
        IEnumerable<PersonalSchedule> personalSchedules)
    {
        if (scheduledCourseChoices == null)
        {
            throw new ArgumentNullException(nameof(scheduledCourseChoices));
        }

        if (unscheduledOfferingSelections == null)
        {
            throw new ArgumentNullException(nameof(unscheduledOfferingSelections));
        }

        if (personalSchedules == null)
        {
            throw new ArgumentNullException(nameof(personalSchedules));
        }

        HashSet<CourseId> selectedCourseIds = new HashSet<CourseId>();
        HashSet<OfferingId> selectedOfferingIds = new HashSet<OfferingId>();
        mScheduledCourseChoices = copyAndValidateScheduledCourseChoices(
            scheduledCourseChoices,
            selectedCourseIds,
            selectedOfferingIds);
        mUnscheduledOfferingSelections = copyAndValidateUnscheduledOfferingSelections(
            unscheduledOfferingSelections,
            selectedCourseIds,
            selectedOfferingIds);
        mPersonalSchedules = copyAndValidatePersonalSchedules(personalSchedules);
    }

    private static IReadOnlyList<ScheduledCourseChoice> copyAndValidateScheduledCourseChoices(
        IEnumerable<ScheduledCourseChoice> scheduledCourseChoices,
        ISet<CourseId> selectedCourseIds,
        ISet<OfferingId> selectedOfferingIds)
    {
        List<ScheduledCourseChoice> copiedChoices = new List<ScheduledCourseChoice>();
        foreach (ScheduledCourseChoice scheduledCourseChoice in scheduledCourseChoices)
        {
            if (scheduledCourseChoice == null)
            {
                throw new ArgumentException(
                    "Planning plans cannot contain null scheduled course choices.",
                    nameof(scheduledCourseChoices));
            }

            if (selectedCourseIds.Add(scheduledCourseChoice.CourseId) == false)
            {
                throw new ArgumentException(
                    "Planning plans cannot select the same course more than once.",
                    nameof(scheduledCourseChoices));
            }

            foreach (OfferingId offeringId in scheduledCourseChoice.OfferingIds)
            {
                if (selectedOfferingIds.Add(offeringId) == false)
                {
                    throw new ArgumentException(
                        "Planning plans cannot select the same offering more than once.",
                        nameof(scheduledCourseChoices));
                }
            }

            copiedChoices.Add(scheduledCourseChoice);
        }

        return copiedChoices.AsReadOnly();
    }

    private static IReadOnlyList<UnscheduledOfferingSelection>
        copyAndValidateUnscheduledOfferingSelections(
            IEnumerable<UnscheduledOfferingSelection> unscheduledOfferingSelections,
            ISet<CourseId> selectedCourseIds,
            ISet<OfferingId> selectedOfferingIds)
    {
        List<UnscheduledOfferingSelection> copiedSelections =
            new List<UnscheduledOfferingSelection>();
        foreach (UnscheduledOfferingSelection selection in unscheduledOfferingSelections)
        {
            if (selection == null)
            {
                throw new ArgumentException(
                    "Planning plans cannot contain null unscheduled selections.",
                    nameof(unscheduledOfferingSelections));
            }

            if (selectedCourseIds.Add(selection.CourseId) == false)
            {
                throw new ArgumentException(
                    "A course cannot be both scheduled and time-unconfirmed in one plan.",
                    nameof(unscheduledOfferingSelections));
            }

            if (selectedOfferingIds.Add(selection.OfferingId) == false)
            {
                throw new ArgumentException(
                    "Planning plans cannot select the same offering more than once.",
                    nameof(unscheduledOfferingSelections));
            }

            copiedSelections.Add(selection);
        }

        return copiedSelections.AsReadOnly();
    }

    private static IReadOnlyList<PersonalSchedule> copyAndValidatePersonalSchedules(
        IEnumerable<PersonalSchedule> personalSchedules)
    {
        List<PersonalSchedule> copiedSchedules = new List<PersonalSchedule>();
        HashSet<PersonalScheduleId> scheduleIds = new HashSet<PersonalScheduleId>();
        foreach (PersonalSchedule personalSchedule in personalSchedules)
        {
            if (personalSchedule == null)
            {
                throw new ArgumentException(
                    "Planning plans cannot contain null personal schedules.",
                    nameof(personalSchedules));
            }

            if (scheduleIds.Add(personalSchedule.Id) == false)
            {
                throw new ArgumentException(
                    "Planning plans cannot contain duplicate personal schedule IDs.",
                    nameof(personalSchedules));
            }

            ensureScheduleDoesNotOverlap(copiedSchedules, personalSchedule);
            copiedSchedules.Add(personalSchedule);
        }

        return copiedSchedules.AsReadOnly();
    }

    private static void ensureScheduleDoesNotOverlap(
        IEnumerable<PersonalSchedule> existingSchedules,
        PersonalSchedule candidateSchedule)
    {
        foreach (PersonalSchedule existingSchedule in existingSchedules)
        {
            foreach (WeeklyTimeRange existingRange in existingSchedule.TimeRanges)
            {
                foreach (WeeklyTimeRange candidateRange in candidateSchedule.TimeRanges)
                {
                    if (ScheduleConflictDetector.HasConflict(
                        existingRange,
                        candidateRange))
                    {
                        throw new ArgumentException(
                            "Personal schedules in one plan cannot overlap.",
                            nameof(candidateSchedule));
                    }
                }
            }
        }
    }
}
