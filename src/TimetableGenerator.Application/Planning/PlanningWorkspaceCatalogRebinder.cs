using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public static class PlanningWorkspaceCatalogRebinder
{
    public static PlanningWorkspaceCatalogRebindResult TryRebind(
        CourseCatalog newCatalog,
        PlanCatalogBinding newBinding,
        PlanningWorkspace workspace)
    {
        if (newCatalog == null)
        {
            throw new ArgumentNullException(nameof(newCatalog));
        }

        if (newBinding == null)
        {
            throw new ArgumentNullException(nameof(newBinding));
        }

        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        requireBindingMatchesCatalog(newCatalog, newBinding);

        if (hasMixedCatalogBindings(workspace))
        {
            return PlanningWorkspaceCatalogRebindResult.createFailure(
                EPlanningWorkspaceCatalogRebindStatus.MixedCatalogBindings);
        }

        PlanCatalogBinding currentBinding = workspace.Plans[0].CatalogBinding;
        EPlanningCatalogTransitionStatus transitionStatus =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                currentBinding,
                newBinding);
        EPlanningWorkspaceCatalogRebindStatus? transitionFailureOrNull =
            getTransitionFailureOrNull(transitionStatus);
        if (transitionFailureOrNull.HasValue)
        {
            return PlanningWorkspaceCatalogRebindResult.createFailure(
                transitionFailureOrNull.Value);
        }

        Dictionary<CourseId, CatalogCourse> coursesById = createCoursesById(newCatalog);
        Dictionary<OfferingId, CatalogOffering> offeringsById =
            createOfferingsById(newCatalog);
        EPlanningWorkspaceCatalogRebindStatus validationStatus = validatePlans(
            workspace,
            coursesById,
            offeringsById);
        if (validationStatus != EPlanningWorkspaceCatalogRebindStatus.Rebound)
        {
            return PlanningWorkspaceCatalogRebindResult.createFailure(validationStatus);
        }

        List<PlanningPlan> reboundPlans = createReboundPlans(workspace, newBinding);
        PlanningWorkspace reboundWorkspace = new PlanningWorkspace(
            workspace.ActivePlanId,
            reboundPlans);
        return PlanningWorkspaceCatalogRebindResult.createRebound(reboundWorkspace);
    }

    private static void requireBindingMatchesCatalog(
        CourseCatalog catalog,
        PlanCatalogBinding binding)
    {
        bool hasMatchingCatalogIdentity = binding.CatalogId == catalog.Id
            && binding.InstitutionId == catalog.InstitutionId
            && binding.Term == catalog.Term
            && binding.Revision == catalog.Revision;
        if (hasMatchingCatalogIdentity == false)
        {
            throw new ArgumentException(
                "The new catalog binding must identify the supplied course catalog.",
                nameof(binding));
        }
    }

    private static EPlanningWorkspaceCatalogRebindStatus?
        getTransitionFailureOrNull(
            EPlanningCatalogTransitionStatus transitionStatus)
    {
        switch (transitionStatus)
        {
            case EPlanningCatalogTransitionStatus.ExactMatch:
            case EPlanningCatalogTransitionStatus.UpgradeEligible:
                return null;
            case EPlanningCatalogTransitionStatus.InstitutionMismatch:
                return EPlanningWorkspaceCatalogRebindStatus.InstitutionMismatch;
            case EPlanningCatalogTransitionStatus.AcademicTermMismatch:
                return EPlanningWorkspaceCatalogRebindStatus.AcademicTermMismatch;
            case EPlanningCatalogTransitionStatus.RevisionNotNewer:
                return EPlanningWorkspaceCatalogRebindStatus.CatalogRevisionNotNewer;
            case EPlanningCatalogTransitionStatus.ArtifactSha256Mismatch:
                return EPlanningWorkspaceCatalogRebindStatus
                    .CatalogArtifactSha256Mismatch;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(transitionStatus),
                    transitionStatus,
                    "Unknown catalog transition status.");
        }
    }

    private static bool hasMixedCatalogBindings(PlanningWorkspace workspace)
    {
        PlanCatalogBinding firstBinding = workspace.Plans[0].CatalogBinding;
        foreach (PlanningPlan plan in workspace.Plans)
        {
            if (plan.CatalogBinding != firstBinding)
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<CourseId, CatalogCourse> createCoursesById(
        CourseCatalog catalog)
    {
        Dictionary<CourseId, CatalogCourse> coursesById =
            new Dictionary<CourseId, CatalogCourse>();
        foreach (CatalogCourse course in catalog.Courses)
        {
            coursesById.Add(course.Id, course);
        }

        return coursesById;
    }

    private static Dictionary<OfferingId, CatalogOffering> createOfferingsById(
        CourseCatalog catalog)
    {
        Dictionary<OfferingId, CatalogOffering> offeringsById =
            new Dictionary<OfferingId, CatalogOffering>();
        foreach (CatalogOffering offering in catalog.Offerings)
        {
            offeringsById.Add(offering.Id, offering);
        }

        return offeringsById;
    }

    private static EPlanningWorkspaceCatalogRebindStatus validatePlans(
        PlanningWorkspace workspace,
        IReadOnlyDictionary<CourseId, CatalogCourse> coursesById,
        IReadOnlyDictionary<OfferingId, CatalogOffering> offeringsById)
    {
        foreach (PlanningPlan plan in workspace.Plans)
        {
            EPlanningWorkspaceCatalogRebindStatus planStatus = validatePlan(
                plan,
                coursesById,
                offeringsById);
            if (planStatus != EPlanningWorkspaceCatalogRebindStatus.Rebound)
            {
                return planStatus;
            }
        }

        return EPlanningWorkspaceCatalogRebindStatus.Rebound;
    }

    private static EPlanningWorkspaceCatalogRebindStatus validatePlan(
        PlanningPlan plan,
        IReadOnlyDictionary<CourseId, CatalogCourse> coursesById,
        IReadOnlyDictionary<OfferingId, CatalogOffering> offeringsById)
    {
        foreach (ScheduledCourseChoice choice in plan.ScheduledCourseChoices)
        {
            EPlanningWorkspaceCatalogRebindStatus choiceStatus = validateScheduledChoice(
                choice,
                coursesById,
                offeringsById);
            if (choiceStatus != EPlanningWorkspaceCatalogRebindStatus.Rebound)
            {
                return choiceStatus;
            }
        }

        foreach (UnscheduledOfferingSelection selection
            in plan.UnscheduledOfferingSelections)
        {
            EPlanningWorkspaceCatalogRebindStatus selectionStatus =
                validateUnscheduledSelection(
                    selection,
                    coursesById,
                    offeringsById);
            if (selectionStatus != EPlanningWorkspaceCatalogRebindStatus.Rebound)
            {
                return selectionStatus;
            }
        }

        return EPlanningWorkspaceCatalogRebindStatus.Rebound;
    }

    private static EPlanningWorkspaceCatalogRebindStatus validateScheduledChoice(
        ScheduledCourseChoice choice,
        IReadOnlyDictionary<CourseId, CatalogCourse> coursesById,
        IReadOnlyDictionary<OfferingId, CatalogOffering> offeringsById)
    {
        if (coursesById.ContainsKey(choice.CourseId) == false)
        {
            return EPlanningWorkspaceCatalogRebindStatus.CourseNotFound;
        }

        foreach (OfferingId offeringId in choice.OfferingIds)
        {
            CatalogOffering? offeringOrNull;
            bool hasOffering = offeringsById.TryGetValue(offeringId, out offeringOrNull);
            if (hasOffering == false || offeringOrNull == null)
            {
                return EPlanningWorkspaceCatalogRebindStatus.OfferingNotFound;
            }

            if (offeringOrNull.CourseId != choice.CourseId)
            {
                return EPlanningWorkspaceCatalogRebindStatus.OfferingCourseMismatch;
            }

            if (offeringOrNull.MeetingSchedule.IsScheduled == false)
            {
                return EPlanningWorkspaceCatalogRebindStatus.ScheduledChoiceHasNoProvidedTime;
            }
        }

        return EPlanningWorkspaceCatalogRebindStatus.Rebound;
    }

    private static EPlanningWorkspaceCatalogRebindStatus validateUnscheduledSelection(
        UnscheduledOfferingSelection selection,
        IReadOnlyDictionary<CourseId, CatalogCourse> coursesById,
        IReadOnlyDictionary<OfferingId, CatalogOffering> offeringsById)
    {
        if (coursesById.ContainsKey(selection.CourseId) == false)
        {
            return EPlanningWorkspaceCatalogRebindStatus.CourseNotFound;
        }

        CatalogOffering? offeringOrNull;
        bool hasOffering = offeringsById.TryGetValue(
            selection.OfferingId,
            out offeringOrNull);
        if (hasOffering == false || offeringOrNull == null)
        {
            return EPlanningWorkspaceCatalogRebindStatus.OfferingNotFound;
        }

        if (offeringOrNull.CourseId != selection.CourseId)
        {
            return EPlanningWorkspaceCatalogRebindStatus.OfferingCourseMismatch;
        }

        if (offeringOrNull.MeetingSchedule.IsScheduled)
        {
            return EPlanningWorkspaceCatalogRebindStatus.UnscheduledSelectionHasProvidedTime;
        }

        return EPlanningWorkspaceCatalogRebindStatus.Rebound;
    }

    private static List<PlanningPlan> createReboundPlans(
        PlanningWorkspace workspace,
        PlanCatalogBinding newBinding)
    {
        List<PlanningPlan> reboundPlans = new List<PlanningPlan>(workspace.Plans.Count);
        foreach (PlanningPlan plan in workspace.Plans)
        {
            PlanningPlan reboundPlan = new PlanningPlan(
                plan.Id,
                plan.Name,
                newBinding,
                plan.Content);
            reboundPlans.Add(reboundPlan);
        }

        return reboundPlans;
    }
}
