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

    public ERecommendationVerificationStatus VerificationStatus { get; }

    internal ScheduleRecommendation(
        IEnumerable<ScheduledOffering> scheduledOfferings,
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections)
    {
        if (scheduledOfferings == null)
        {
            throw new ArgumentNullException(nameof(scheduledOfferings));
        }

        if (unscheduledSelections == null)
        {
            throw new ArgumentNullException(nameof(unscheduledSelections));
        }

        IReadOnlyList<ScheduledOffering> copiedScheduledOfferings =
            copyAndValidateScheduledOfferings(scheduledOfferings);
        IReadOnlyList<UnscheduledOfferingSelection> copiedUnscheduledSelections =
            copyUnscheduledSelections(unscheduledSelections);
        if (copiedScheduledOfferings.Count == 0 && copiedUnscheduledSelections.Count == 0)
        {
            throw new ArgumentException(
                "Schedule recommendations require at least one selected item.");
        }

        mScheduledOfferings = copiedScheduledOfferings;
        mUnscheduledSelections = copiedUnscheduledSelections;
        VerificationStatus = copiedUnscheduledSelections.Count == 0
            ? ERecommendationVerificationStatus.ConfirmedConflictFree
            : ERecommendationVerificationStatus.RequiresManualReview;
    }

    private static IReadOnlyList<ScheduledOffering> copyAndValidateScheduledOfferings(
        IEnumerable<ScheduledOffering> scheduledOfferings)
    {
        List<ScheduledOffering> copiedOfferings = new List<ScheduledOffering>();
        HashSet<CourseId> selectedCourseIds = new HashSet<CourseId>();
        HashSet<OfferingId> selectedOfferingIds = new HashSet<OfferingId>();
        foreach (ScheduledOffering scheduledOffering in scheduledOfferings)
        {
            if (scheduledOffering == null)
            {
                throw new ArgumentException(
                    "Schedule recommendations cannot contain null offerings.",
                    nameof(scheduledOfferings));
            }

            if (selectedCourseIds.Add(scheduledOffering.CourseId) == false)
            {
                throw new ArgumentException(
                    "Schedule recommendations can select only one offering per course.",
                    nameof(scheduledOfferings));
            }

            if (selectedOfferingIds.Add(scheduledOffering.OfferingId) == false)
            {
                throw new ArgumentException(
                    "Schedule recommendations cannot contain duplicate offerings.",
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
        List<UnscheduledOfferingSelection> copiedSelections =
            new List<UnscheduledOfferingSelection>();
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
}
