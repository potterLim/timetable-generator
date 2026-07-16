using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class PlanCatalogValidationResult
{
    private readonly IReadOnlyList<ValidatedScheduleChoice> mScheduledChoices;

    private readonly IReadOnlyList<UnscheduledOfferingSelection> mUnscheduledSelections;

    public IReadOnlyList<ValidatedScheduleChoice> ScheduledChoices
    {
        get
        {
            return mScheduledChoices;
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
        IEnumerable<ValidatedScheduleChoice> scheduledChoices,
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections,
        EPlanCatalogValidationError error)
    {
        if (scheduledChoices == null)
        {
            throw new ArgumentNullException(nameof(scheduledChoices));
        }

        if (unscheduledSelections == null)
        {
            throw new ArgumentNullException(nameof(unscheduledSelections));
        }

        if (Enum.IsDefined(typeof(EPlanCatalogValidationError), error) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }

        mScheduledChoices = copyScheduledChoices(scheduledChoices);
        mUnscheduledSelections = copyUnscheduledSelections(unscheduledSelections);
        Error = error;
    }

    public static PlanCatalogValidationResult CreateValid(
        IEnumerable<ValidatedScheduleChoice> scheduledChoices,
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections)
    {
        return new PlanCatalogValidationResult(
            scheduledChoices,
            unscheduledSelections,
            EPlanCatalogValidationError.None);
    }

    public static PlanCatalogValidationResult CreateInvalid(
        EPlanCatalogValidationError error)
    {
        if (error == EPlanCatalogValidationError.None)
        {
            throw new ArgumentOutOfRangeException(nameof(error));
        }

        return new PlanCatalogValidationResult(
            Array.Empty<ValidatedScheduleChoice>(),
            Array.Empty<UnscheduledOfferingSelection>(),
            error);
    }

    private static IReadOnlyList<ValidatedScheduleChoice> copyScheduledChoices(
        IEnumerable<ValidatedScheduleChoice> scheduledChoices)
    {
        List<ValidatedScheduleChoice> copiedChoices = new List<ValidatedScheduleChoice>();
        foreach (ValidatedScheduleChoice choice in scheduledChoices)
        {
            copiedChoices.Add(choice);
        }

        return copiedChoices.AsReadOnly();
    }

    private static IReadOnlyList<UnscheduledOfferingSelection> copyUnscheduledSelections(
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections)
    {
        List<UnscheduledOfferingSelection> copiedSelections =
            new List<UnscheduledOfferingSelection>();
        foreach (UnscheduledOfferingSelection selection in unscheduledSelections)
        {
            copiedSelections.Add(selection);
        }

        return copiedSelections.AsReadOnly();
    }
}
