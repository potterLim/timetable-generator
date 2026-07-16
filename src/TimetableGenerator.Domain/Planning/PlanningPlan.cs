using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed class PlanningPlan
{
    private readonly IReadOnlyList<ScheduledCourseChoice> mScheduledCourseChoices;

    private readonly IReadOnlyList<UnscheduledOfferingSelection> mUnscheduledOfferingSelections;

    public PlanId Id { get; }

    public PlanName Name { get; }

    public PlanCatalogBinding CatalogBinding { get; }

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

    public bool HasUnscheduledOfferingSelections
    {
        get
        {
            return mUnscheduledOfferingSelections.Count > 0;
        }
    }

    public PlanningPlan(
        PlanId id,
        PlanName name,
        PlanCatalogBinding catalogBinding,
        IEnumerable<ScheduledCourseChoice> scheduledCourseChoices,
        IEnumerable<UnscheduledOfferingSelection> unscheduledOfferingSelections)
    {
        if (id.IsValid == false)
        {
            throw new ArgumentException("Planning plans require a valid ID.", nameof(id));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (catalogBinding == null)
        {
            throw new ArgumentNullException(nameof(catalogBinding));
        }

        if (scheduledCourseChoices == null)
        {
            throw new ArgumentNullException(nameof(scheduledCourseChoices));
        }

        if (unscheduledOfferingSelections == null)
        {
            throw new ArgumentNullException(nameof(unscheduledOfferingSelections));
        }

        HashSet<CourseId> selectedCourseIds = new HashSet<CourseId>();
        HashSet<OfferingId> selectedOfferingIds = new HashSet<OfferingId>();
        IReadOnlyList<ScheduledCourseChoice> copiedScheduledCourseChoices =
            copyAndValidateScheduledCourseChoices(
                scheduledCourseChoices,
                selectedCourseIds,
                selectedOfferingIds);
        IReadOnlyList<UnscheduledOfferingSelection> copiedUnscheduledSelections =
            copyAndValidateUnscheduledOfferingSelections(
                unscheduledOfferingSelections,
                selectedCourseIds,
                selectedOfferingIds);

        Id = id;
        Name = name;
        CatalogBinding = catalogBinding;
        mScheduledCourseChoices = copiedScheduledCourseChoices;
        mUnscheduledOfferingSelections = copiedUnscheduledSelections;
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
}
