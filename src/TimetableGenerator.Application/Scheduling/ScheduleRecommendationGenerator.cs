using System;
using System.Collections.Generic;
using System.Threading;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

public sealed class ScheduleRecommendationGenerator
{
    public ScheduleRecommendationResult GenerateRecommendations(
        ScheduleRecommendationRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return ScheduleRecommendationResult.createCanceled(
                Array.Empty<ScheduleRecommendation>());
        }

        PlanCatalogValidationResult validationResult = validatePlanReferences(request);
        if (validationResult.IsValid == false)
        {
            return ScheduleRecommendationResult.createInvalidPlan(validationResult.Error);
        }

        if (validationResult.ScheduledChoices.Count == 0)
        {
            return createResultWithoutScheduledChoices(validationResult.UnscheduledSelections);
        }

        ScheduleRecommendationGenerationState state =
            new ScheduleRecommendationGenerationState(
                validationResult.ScheduledChoices,
                validationResult.UnscheduledSelections,
                request.MaximumRecommendationCount,
                cancellationToken);
        generateRecommendationsRecursive(state, 0);

        if (state.Completion == EScheduleRecommendationCompletion.Canceled)
        {
            return ScheduleRecommendationResult.createCanceled(state.Recommendations);
        }

        return ScheduleRecommendationResult.createCompleted(
            state.Recommendations,
            state.Completion);
    }

    private static ScheduleRecommendationResult createResultWithoutScheduledChoices(
        IReadOnlyList<UnscheduledOfferingSelection> unscheduledSelections)
    {
        if (unscheduledSelections.Count == 0)
        {
            return ScheduleRecommendationResult.createCompleted(
                Array.Empty<ScheduleRecommendation>(),
                EScheduleRecommendationCompletion.Completed);
        }

        ScheduleRecommendation recommendation = new ScheduleRecommendation(
            Array.Empty<ScheduledOffering>(),
            unscheduledSelections);
        return ScheduleRecommendationResult.createCompleted(
            new ScheduleRecommendation[] { recommendation },
            EScheduleRecommendationCompletion.Completed);
    }

    private static PlanCatalogValidationResult validatePlanReferences(
        ScheduleRecommendationRequest request)
    {
        EPlanCatalogValidationError bindingError = validateCatalogBinding(
            request.Catalog,
            request.Plan.CatalogBinding);
        if (bindingError != EPlanCatalogValidationError.None)
        {
            return PlanCatalogValidationResult.CreateInvalid(bindingError);
        }

        Dictionary<CourseId, CatalogCourse> coursesById = createCoursesById(request.Catalog);
        Dictionary<OfferingId, CatalogOffering> offeringsById = createOfferingsById(
            request.Catalog);
        List<ValidatedScheduleChoice> validatedChoices =
            new List<ValidatedScheduleChoice>();

        foreach (ScheduledCourseChoice choice in request.Plan.ScheduledCourseChoices)
        {
            EPlanCatalogValidationError choiceError = validateScheduledChoice(
                choice,
                coursesById,
                offeringsById,
                validatedChoices);
            if (choiceError != EPlanCatalogValidationError.None)
            {
                return PlanCatalogValidationResult.CreateInvalid(choiceError);
            }
        }

        foreach (UnscheduledOfferingSelection selection
            in request.Plan.UnscheduledOfferingSelections)
        {
            EPlanCatalogValidationError selectionError = validateUnscheduledSelection(
                selection,
                coursesById,
                offeringsById);
            if (selectionError != EPlanCatalogValidationError.None)
            {
                return PlanCatalogValidationResult.CreateInvalid(selectionError);
            }
        }

        return PlanCatalogValidationResult.CreateValid(
            validatedChoices,
            request.Plan.UnscheduledOfferingSelections);
    }

    private static EPlanCatalogValidationError validateCatalogBinding(
        CourseCatalog catalog,
        PlanCatalogBinding catalogBinding)
    {
        bool hasMatchingCatalogId = catalog.Id == catalogBinding.CatalogId;
        bool hasMatchingTerm = catalog.Term == catalogBinding.Term;
        bool hasMatchingRevision = catalog.Revision == catalogBinding.Revision;
        if (hasMatchingCatalogId == false
            || hasMatchingTerm == false
            || hasMatchingRevision == false)
        {
            return EPlanCatalogValidationError.CatalogBindingMismatch;
        }

        return EPlanCatalogValidationError.None;
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

    private static EPlanCatalogValidationError validateScheduledChoice(
        ScheduledCourseChoice choice,
        IReadOnlyDictionary<CourseId, CatalogCourse> coursesById,
        IReadOnlyDictionary<OfferingId, CatalogOffering> offeringsById,
        ICollection<ValidatedScheduleChoice> validatedChoices)
    {
        if (coursesById.ContainsKey(choice.CourseId) == false)
        {
            return EPlanCatalogValidationError.CourseNotFound;
        }

        List<ScheduledOffering> scheduledOfferings = new List<ScheduledOffering>();
        foreach (OfferingId offeringId in choice.OfferingIds)
        {
            CatalogOffering? catalogOfferingOrNull;
            bool hasOffering = offeringsById.TryGetValue(
                offeringId,
                out catalogOfferingOrNull);
            if (hasOffering == false || catalogOfferingOrNull == null)
            {
                return EPlanCatalogValidationError.OfferingNotFound;
            }

            if (catalogOfferingOrNull.CourseId != choice.CourseId)
            {
                return EPlanCatalogValidationError.OfferingCourseMismatch;
            }

            if (catalogOfferingOrNull.MeetingSchedule.IsScheduled == false)
            {
                return EPlanCatalogValidationError.ScheduledChoiceHasNoProvidedTime;
            }

            scheduledOfferings.Add(new ScheduledOffering(catalogOfferingOrNull));
        }

        validatedChoices.Add(new ValidatedScheduleChoice(scheduledOfferings));
        return EPlanCatalogValidationError.None;
    }

    private static EPlanCatalogValidationError validateUnscheduledSelection(
        UnscheduledOfferingSelection selection,
        IReadOnlyDictionary<CourseId, CatalogCourse> coursesById,
        IReadOnlyDictionary<OfferingId, CatalogOffering> offeringsById)
    {
        if (coursesById.ContainsKey(selection.CourseId) == false)
        {
            return EPlanCatalogValidationError.CourseNotFound;
        }

        CatalogOffering? catalogOfferingOrNull;
        bool hasOffering = offeringsById.TryGetValue(
            selection.OfferingId,
            out catalogOfferingOrNull);
        if (hasOffering == false || catalogOfferingOrNull == null)
        {
            return EPlanCatalogValidationError.OfferingNotFound;
        }

        if (catalogOfferingOrNull.CourseId != selection.CourseId)
        {
            return EPlanCatalogValidationError.OfferingCourseMismatch;
        }

        if (catalogOfferingOrNull.MeetingSchedule.IsScheduled)
        {
            return EPlanCatalogValidationError.UnscheduledSelectionHasProvidedTime;
        }

        return EPlanCatalogValidationError.None;
    }

    private static EGenerationTraversalDecision generateRecommendationsRecursive(
        ScheduleRecommendationGenerationState state,
        int choiceIndex)
    {
        if (state.ShouldStop)
        {
            return EGenerationTraversalDecision.Stop;
        }

        if (state.CancellationToken.IsCancellationRequested)
        {
            state.MarkCanceled();
            return EGenerationTraversalDecision.Stop;
        }

        if (choiceIndex >= state.ScheduledChoices.Count)
        {
            return addCompletedRecommendation(state);
        }

        ValidatedScheduleChoice choice = state.ScheduledChoices[choiceIndex];
        foreach (ScheduledOffering offering in choice.Offerings)
        {
            if (state.CancellationToken.IsCancellationRequested)
            {
                state.MarkCanceled();
                return EGenerationTraversalDecision.Stop;
            }

            if (canAddOffering(state, offering) == false)
            {
                continue;
            }

            addOffering(state, offering);
            try
            {
                EGenerationTraversalDecision traversalDecision =
                    generateRecommendationsRecursive(state, choiceIndex + 1);
                if (traversalDecision == EGenerationTraversalDecision.Stop)
                {
                    return EGenerationTraversalDecision.Stop;
                }
            }
            finally
            {
                removeOffering(state, offering);
            }
        }

        return EGenerationTraversalDecision.Continue;
    }

    private static EGenerationTraversalDecision addCompletedRecommendation(
        ScheduleRecommendationGenerationState state)
    {
        if (state.Recommendations.Count >= state.MaximumRecommendationCount.Value)
        {
            state.MarkMaximumRecommendationCountReached();
            return EGenerationTraversalDecision.Stop;
        }

        ScheduleRecommendation recommendation = new ScheduleRecommendation(
            state.SelectedOfferings,
            state.UnscheduledSelections);
        state.Recommendations.Add(recommendation);
        return EGenerationTraversalDecision.Continue;
    }

    private static bool canAddOffering(
        ScheduleRecommendationGenerationState state,
        ScheduledOffering offering)
    {
        foreach (MeetingSlot slot in offering.MeetingSlots)
        {
            if (state.OccupiedSlots.Contains(slot))
            {
                return false;
            }
        }

        return true;
    }

    private static void addOffering(
        ScheduleRecommendationGenerationState state,
        ScheduledOffering offering)
    {
        state.SelectedOfferings.Add(offering);
        foreach (MeetingSlot slot in offering.MeetingSlots)
        {
            bool hasAddedSlot = state.OccupiedSlots.Add(slot);
            if (hasAddedSlot == false)
            {
                throw new InvalidOperationException(
                    "A previously validated meeting slot could not be reserved.");
            }
        }
    }

    private static void removeOffering(
        ScheduleRecommendationGenerationState state,
        ScheduledOffering offering)
    {
        int selectedOfferingIndex = state.SelectedOfferings.Count - 1;
        ScheduledOffering selectedOffering = state.SelectedOfferings[selectedOfferingIndex];
        if (ReferenceEquals(selectedOffering, offering) == false)
        {
            throw new InvalidOperationException(
                "The schedule recommendation rollback order was corrupted.");
        }

        state.SelectedOfferings.RemoveAt(selectedOfferingIndex);
        foreach (MeetingSlot slot in offering.MeetingSlots)
        {
            bool hasRemovedSlot = state.OccupiedSlots.Remove(slot);
            if (hasRemovedSlot == false)
            {
                throw new InvalidOperationException(
                    "A reserved meeting slot could not be released.");
            }
        }
    }
}
