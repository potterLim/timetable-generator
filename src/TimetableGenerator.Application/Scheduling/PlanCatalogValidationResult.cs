using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class PlanCatalogValidationResult
{
    private readonly IReadOnlyList<ValidatedCourseChoiceGroup> mCourseChoiceGroups;

    private readonly IReadOnlyList<UnscheduledOfferingSelection> mUnscheduledSelections;

    public IReadOnlyList<ValidatedCourseChoiceGroup> CourseChoiceGroups
    {
        get
        {
            return mCourseChoiceGroups;
        }
    }

    public IReadOnlyList<UnscheduledOfferingSelection> UnscheduledSelections
    {
        get
        {
            return mUnscheduledSelections;
        }
    }

    public EPlanCatalogValidationError Error { get; }

    public bool IsValid
    {
        get
        {
            return Error == EPlanCatalogValidationError.None;
        }
    }

    private PlanCatalogValidationResult(
        IEnumerable<ValidatedCourseChoiceGroup> courseChoiceGroups,
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections,
        EPlanCatalogValidationError error)
    {
        if (courseChoiceGroups == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroups));
        }

        if (unscheduledSelections == null)
        {
            throw new ArgumentNullException(nameof(unscheduledSelections));
        }

        if (Enum.IsDefined(typeof(EPlanCatalogValidationError), error) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }

        mCourseChoiceGroups = copyCourseChoiceGroups(courseChoiceGroups);
        mUnscheduledSelections = copyUnscheduledSelections(unscheduledSelections);
        Error = error;
    }

    public static PlanCatalogValidationResult CreateValid(
        IEnumerable<ValidatedCourseChoiceGroup> courseChoiceGroups,
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections)
    {
        return new PlanCatalogValidationResult(
            courseChoiceGroups,
            unscheduledSelections,
            EPlanCatalogValidationError.None);
    }

    public static PlanCatalogValidationResult CreateInvalid(EPlanCatalogValidationError error)
    {
        if (error == EPlanCatalogValidationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }

        return new PlanCatalogValidationResult(
            Array.Empty<ValidatedCourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>(),
            error);
    }

    private static IReadOnlyList<ValidatedCourseChoiceGroup> copyCourseChoiceGroups(
        IEnumerable<ValidatedCourseChoiceGroup> courseChoiceGroups)
    {
        List<ValidatedCourseChoiceGroup> copiedGroups = new List<ValidatedCourseChoiceGroup>();
        foreach (ValidatedCourseChoiceGroup courseChoiceGroup in courseChoiceGroups)
        {
            copiedGroups.Add(courseChoiceGroup);
        }

        return copiedGroups.AsReadOnly();
    }

    private static IReadOnlyList<UnscheduledOfferingSelection> copyUnscheduledSelections(
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections)
    {
        List<UnscheduledOfferingSelection> copiedSelections = new List<UnscheduledOfferingSelection>();
        foreach (UnscheduledOfferingSelection selection in unscheduledSelections)
        {
            copiedSelections.Add(selection);
        }

        return copiedSelections.AsReadOnly();
    }
}
