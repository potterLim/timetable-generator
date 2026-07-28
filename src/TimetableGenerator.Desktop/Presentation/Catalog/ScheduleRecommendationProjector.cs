using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;
using ApplicationScheduleRecommendation = TimetableGenerator.Application.Scheduling.ScheduleRecommendation;
using PresentationScheduleRecommendation = TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Presentation.Catalog;

internal static class ScheduleRecommendationProjector
{
    public static PresentationScheduleRecommendation ProjectPersonalSchedules(IEnumerable<PersonalSchedule> personalSchedules)
    {
        if (personalSchedules == null)
        {
            throw new ArgumentNullException(nameof(personalSchedules));
        }

        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        foreach (PersonalSchedule personalSchedule in personalSchedules)
        {
            if (personalSchedule == null)
            {
                throw new ArgumentException("Schedule projections cannot contain null personal schedules.", nameof(personalSchedules));
            }

            foreach (WeeklyTimeRange timeRange in personalSchedule.TimeRanges)
            {
                entries.Add(new PersonalScheduleEntry(personalSchedule, timeRange));
            }
        }

        entries.Sort(compareScheduleEntries);
        return new PresentationScheduleRecommendation(entries);
    }

    public static PresentationScheduleRecommendation Project(ApplicationScheduleRecommendation recommendation, CourseCatalogProjection catalogProjection)
    {
        if (recommendation == null)
        {
            throw new ArgumentNullException(nameof(recommendation));
        }

        if (catalogProjection == null)
        {
            throw new ArgumentNullException(nameof(catalogProjection));
        }

        HashSet<CourseId> selectedCourseIds = new HashSet<CourseId>();
        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        foreach (ScheduledOffering scheduledOffering in recommendation.ScheduledOfferings)
        {
            validateScheduledOffering(scheduledOffering, catalogProjection, selectedCourseIds);
            addScheduleEntries(scheduledOffering, catalogProjection, entries);
        }

        foreach (UnscheduledOfferingSelection selection in recommendation.UnscheduledSelections)
        {
            validateUnscheduledSelection(selection, catalogProjection, selectedCourseIds);
        }

        addPersonalScheduleEntries(recommendation.PersonalSchedules, entries);

        entries.Sort(compareScheduleEntries);
        return new PresentationScheduleRecommendation(entries);
    }

    private static void addPersonalScheduleEntries(IEnumerable<PersonalSchedule> personalSchedules, ICollection<ScheduleEntry> entries)
    {
        foreach (PersonalSchedule personalSchedule in personalSchedules)
        {
            foreach (WeeklyTimeRange timeRange in personalSchedule.TimeRanges)
            {
                entries.Add(new PersonalScheduleEntry(personalSchedule, timeRange));
            }
        }
    }

    private static void validateScheduledOffering(ScheduledOffering scheduledOffering, CourseCatalogProjection catalogProjection, ISet<CourseId> selectedCourseIds)
    {
        if (catalogProjection.HasOffering(scheduledOffering.OfferingId) == false)
        {
            throw new ArgumentException("The recommendation references an offering outside the projected catalog.", nameof(scheduledOffering));
        }

        CatalogOffering sourceOffering = catalogProjection.FindOfferingById(scheduledOffering.OfferingId).Offering;
        bool hasMatchingIdentity = sourceOffering.CourseId == scheduledOffering.CourseId && sourceOffering.SectionCode == scheduledOffering.SectionCode;
        if (hasMatchingIdentity == false)
        {
            throw new ArgumentException("The recommendation offering identity does not match the projected catalog.", nameof(scheduledOffering));
        }

        if (sourceOffering.MeetingSchedule.IsScheduled == false || haveMatchingSlots(sourceOffering.MeetingSchedule.Slots, scheduledOffering.MeetingSlots) == false)
        {
            throw new ArgumentException("The recommendation schedule does not match the projected catalog.", nameof(scheduledOffering));
        }

        if (selectedCourseIds.Add(scheduledOffering.CourseId) == false)
        {
            throw new ArgumentException("A recommendation cannot select a course more than once.", nameof(scheduledOffering));
        }
    }

    private static void validateUnscheduledSelection(UnscheduledOfferingSelection selection, CourseCatalogProjection catalogProjection, ISet<CourseId> selectedCourseIds)
    {
        if (catalogProjection.HasOffering(selection.OfferingId) == false)
        {
            throw new ArgumentException("The recommendation references an offering outside the projected catalog.", nameof(selection));
        }

        CatalogOffering sourceOffering = catalogProjection.FindOfferingById(selection.OfferingId).Offering;
        if (sourceOffering.CourseId != selection.CourseId)
        {
            throw new ArgumentException("The time-not-provided selection does not belong to its declared course.", nameof(selection));
        }

        if (sourceOffering.MeetingSchedule.Status != EMeetingScheduleStatus.NotProvided)
        {
            throw new ArgumentException("Time-not-provided selections must reference offerings without a schedule.", nameof(selection));
        }

        if (selectedCourseIds.Add(selection.CourseId) == false)
        {
            throw new ArgumentException("A recommendation cannot select a course more than once.", nameof(selection));
        }
    }

    private static void addScheduleEntries(ScheduledOffering scheduledOffering, CourseCatalogProjection catalogProjection, ICollection<ScheduleEntry> entries)
    {
        CatalogCourseProjection courseProjection = catalogProjection.FindCourseById(scheduledOffering.CourseId);
        CatalogOfferingProjection offeringProjection = catalogProjection.FindOfferingById(scheduledOffering.OfferingId);
        ScheduleCourseDetails courseDetails = new ScheduleCourseDetails(
            courseProjection.Course.Code,
            courseProjection.Course.KoreanName,
            courseProjection.Course.Credits,
            new ScheduleInstructorSummary(offeringProjection.Metadata.Instruction.InstructorAssignment),
            new ScheduleLocationSummary(offeringProjection.Metadata.Logistics.Location));

        foreach (MeetingSlot slot in scheduledOffering.MeetingSlots)
        {
            entries.Add(new CourseScheduleEntry(
                scheduledOffering.CourseId,
                scheduledOffering.OfferingId,
                courseDetails,
                scheduledOffering.SectionCode,
                slot,
                courseProjection.Accent));
        }
    }

    private static bool haveMatchingSlots(IReadOnlyList<MeetingSlot> sourceSlots, IReadOnlyList<MeetingSlot> recommendationSlots)
    {
        if (sourceSlots.Count != recommendationSlots.Count)
        {
            return false;
        }

        HashSet<MeetingSlot> sourceSlotSet = new HashSet<MeetingSlot>(sourceSlots);
        return sourceSlotSet.SetEquals(recommendationSlots);
    }

    private static int compareScheduleEntries(ScheduleEntry left, ScheduleEntry right)
    {
        int startComparison = left.TimeRange.Start.CompareTo(right.TimeRange.Start);
        if (startComparison != 0)
        {
            return startComparison;
        }

        int dayComparison = left.Day.CompareTo(right.Day);
        if (dayComparison != 0)
        {
            return dayComparison;
        }

        return string.Compare(getEntrySortName(left), getEntrySortName(right), StringComparison.Ordinal);
    }

    private static string getEntrySortName(ScheduleEntry entry)
    {
        CourseScheduleEntry? courseEntryOrNull = entry as CourseScheduleEntry;
        if (courseEntryOrNull != null)
        {
            return courseEntryOrNull.Code;
        }

        PersonalScheduleEntry? personalEntryOrNull = entry as PersonalScheduleEntry;
        if (personalEntryOrNull != null)
        {
            return personalEntryOrNull.Title;
        }

        throw new ArgumentOutOfRangeException(nameof(entry), entry, "Unknown schedule entry type.");
    }
}
