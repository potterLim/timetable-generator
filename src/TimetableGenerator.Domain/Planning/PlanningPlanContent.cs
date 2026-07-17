using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Domain.Planning;

public sealed class PlanningPlanContent
{
    private readonly IReadOnlyList<CourseChoiceGroup> mCourseChoiceGroups;

    private readonly IReadOnlyList<UnscheduledOfferingSelection>
        mUnscheduledOfferingSelections;

    private readonly IReadOnlyList<PersonalSchedule> mPersonalSchedules;

    public IReadOnlyList<CourseChoiceGroup> CourseChoiceGroups
    {
        get
        {
            return mCourseChoiceGroups;
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
        IEnumerable<CourseChoiceGroup> courseChoiceGroups,
        IEnumerable<UnscheduledOfferingSelection> unscheduledOfferingSelections,
        IEnumerable<PersonalSchedule> personalSchedules)
    {
        if (courseChoiceGroups == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroups));
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
        mCourseChoiceGroups = copyAndValidateCourseChoiceGroups(
            courseChoiceGroups,
            selectedCourseIds,
            selectedOfferingIds);
        mUnscheduledOfferingSelections = copyAndValidateUnscheduledOfferingSelections(
            unscheduledOfferingSelections,
            selectedCourseIds,
            selectedOfferingIds);
        mPersonalSchedules = copyAndValidatePersonalSchedules(personalSchedules);
    }

    private static IReadOnlyList<CourseChoiceGroup> copyAndValidateCourseChoiceGroups(
        IEnumerable<CourseChoiceGroup> courseChoiceGroups,
        ISet<CourseId> selectedCourseIds,
        ISet<OfferingId> selectedOfferingIds)
    {
        List<CourseChoiceGroup> copiedGroups = new List<CourseChoiceGroup>();
        HashSet<CourseChoiceGroupId> groupIds = new HashSet<CourseChoiceGroupId>();
        foreach (CourseChoiceGroup courseChoiceGroup in courseChoiceGroups)
        {
            if (courseChoiceGroup == null)
            {
                throw new ArgumentException(
                    "Planning plans cannot contain null course choice groups.",
                    nameof(courseChoiceGroups));
            }

            if (groupIds.Add(courseChoiceGroup.Id) == false)
            {
                throw new ArgumentException(
                    "Planning plans cannot contain duplicate course choice group IDs.",
                    nameof(courseChoiceGroups));
            }

            foreach (CourseCandidate courseCandidate
                in courseChoiceGroup.CourseCandidates)
            {
                if (selectedCourseIds.Add(courseCandidate.CourseId) == false)
                {
                    throw new ArgumentException(
                        "Planning plans cannot select the same course more than once.",
                        nameof(courseChoiceGroups));
                }

                foreach (OfferingCandidate offeringCandidate
                    in courseCandidate.OfferingCandidates)
                {
                    if (selectedOfferingIds.Add(offeringCandidate.OfferingId) == false)
                    {
                        throw new ArgumentException(
                            "Planning plans cannot select the same offering more than once.",
                            nameof(courseChoiceGroups));
                    }
                }
            }

            copiedGroups.Add(courseChoiceGroup);
        }

        return copiedGroups.AsReadOnly();
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
