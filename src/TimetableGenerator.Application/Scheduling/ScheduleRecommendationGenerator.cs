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

        if (validationResult.CourseChoiceGroups.Count == 0)
        {
            return createResultWithoutScheduledChoices(
                validationResult.UnscheduledSelections,
                request.Plan.PersonalSchedules);
        }

        ScheduleRecommendationGenerationState state =
            new ScheduleRecommendationGenerationState(
                validationResult.CourseChoiceGroups,
                validationResult.UnscheduledSelections,
                request.Plan.PersonalSchedules,
                request.MaximumRecommendationCount,
                cancellationToken);
        generateRecommendations(state);

        if (state.Completion == EScheduleRecommendationCompletion.Canceled)
        {
            return ScheduleRecommendationResult.createCanceled(state.Recommendations);
        }

        return ScheduleRecommendationResult.createCompleted(
            state.Recommendations,
            state.Completion);
    }

    private static ScheduleRecommendationResult createResultWithoutScheduledChoices(
        IReadOnlyList<UnscheduledOfferingSelection> unscheduledSelections,
        IReadOnlyList<PersonalSchedule> personalSchedules)
    {
        if (unscheduledSelections.Count == 0 && personalSchedules.Count == 0)
        {
            return ScheduleRecommendationResult.createCompleted(
                Array.Empty<ScheduleRecommendation>(),
                EScheduleRecommendationCompletion.Completed);
        }

        ScheduleRecommendation recommendation = new ScheduleRecommendation(
            Array.Empty<ScheduledOffering>(),
            unscheduledSelections,
            personalSchedules,
            RecommendationScore.ZERO);
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
        List<ValidatedCourseChoiceGroup> validatedGroups =
            new List<ValidatedCourseChoiceGroup>();

        foreach (CourseChoiceGroup courseChoiceGroup
            in request.Plan.CourseChoiceGroups)
        {
            EPlanCatalogValidationError choiceError = validateCourseChoiceGroup(
                courseChoiceGroup,
                coursesById,
                offeringsById,
                validatedGroups);
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
            validatedGroups,
            request.Plan.UnscheduledOfferingSelections);
    }

    private static EPlanCatalogValidationError validateCatalogBinding(
        CourseCatalog catalog,
        PlanCatalogBinding catalogBinding)
    {
        bool hasMatchingCatalogId = catalog.Id == catalogBinding.CatalogId;
        bool hasMatchingInstitutionId =
            catalog.InstitutionId == catalogBinding.InstitutionId;
        bool hasMatchingTerm = catalog.Term == catalogBinding.Term;
        bool hasMatchingRevision = catalog.Revision == catalogBinding.Revision;
        if (hasMatchingCatalogId == false
            || hasMatchingInstitutionId == false
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

    private static EPlanCatalogValidationError validateCourseChoiceGroup(
        CourseChoiceGroup courseChoiceGroup,
        IReadOnlyDictionary<CourseId, CatalogCourse> coursesById,
        IReadOnlyDictionary<OfferingId, CatalogOffering> offeringsById,
        ICollection<ValidatedCourseChoiceGroup> validatedGroups)
    {
        List<ValidatedOfferingCandidate> validatedCandidates =
            new List<ValidatedOfferingCandidate>();
        foreach (CourseCandidate courseCandidate
            in courseChoiceGroup.CourseCandidates)
        {
            if (coursesById.ContainsKey(courseCandidate.CourseId) == false)
            {
                return EPlanCatalogValidationError.CourseNotFound;
            }

            foreach (OfferingCandidate offeringCandidate
                in courseCandidate.OfferingCandidates)
            {
                CatalogOffering? catalogOfferingOrNull;
                bool hasOffering = offeringsById.TryGetValue(
                    offeringCandidate.OfferingId,
                    out catalogOfferingOrNull);
                if (hasOffering == false || catalogOfferingOrNull == null)
                {
                    return EPlanCatalogValidationError.OfferingNotFound;
                }

                if (catalogOfferingOrNull.CourseId != courseCandidate.CourseId)
                {
                    return EPlanCatalogValidationError.OfferingCourseMismatch;
                }

                if (catalogOfferingOrNull.MeetingSchedule.IsScheduled == false)
                {
                    return EPlanCatalogValidationError
                        .ScheduledChoiceHasNoProvidedTime;
                }

                if (offeringCandidate.IsEligible)
                {
                    validatedCandidates.Add(new ValidatedOfferingCandidate(
                        new ScheduledOffering(catalogOfferingOrNull),
                        offeringCandidate.Preference));
                }
            }
        }

        validatedGroups.Add(new ValidatedCourseChoiceGroup(validatedCandidates));
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

    private static void generateRecommendations(
        ScheduleRecommendationGenerationState state)
    {
        while (state.HasPendingNodes() && state.ShouldStop == false)
        {
            if (state.CancellationToken.IsCancellationRequested)
            {
                state.MarkCanceled();
                return;
            }

            ScheduleSearchNode node = state.DequeueNode();
            if (node.NextGroupIndex >= state.CourseChoiceGroups.Count)
            {
                bool hasReachedLimit = addCompletedRecommendation(state, node);
                if (hasReachedLimit)
                {
                    state.MarkMaximumRecommendationCountReached();
                    return;
                }

                continue;
            }

            ValidatedCourseChoiceGroup courseChoiceGroup =
                state.CourseChoiceGroups[node.NextGroupIndex];
            foreach (ValidatedOfferingCandidate offeringCandidate
                in courseChoiceGroup.OfferingCandidates)
            {
                if (state.CancellationToken.IsCancellationRequested)
                {
                    state.MarkCanceled();
                    return;
                }

                if (canAddOffering(
                    node,
                    offeringCandidate.Offering,
                    state.PersonalSchedules) == false)
                {
                    continue;
                }

                ScheduleSearchNode childNode = node.CreateChild(offeringCandidate);
                state.EnqueueNode(childNode);
            }
        }
    }

    private static bool addCompletedRecommendation(
        ScheduleRecommendationGenerationState state,
        ScheduleSearchNode node)
    {
        if (state.Recommendations.Count >= state.MaximumRecommendationCount.Value)
        {
            return true;
        }

        ScheduleRecommendation recommendation = new ScheduleRecommendation(
            node.SelectedOfferings,
            state.UnscheduledSelections,
            state.PersonalSchedules,
            node.Score);
        state.Recommendations.Add(recommendation);
        return false;
    }

    private static bool canAddOffering(
        ScheduleSearchNode node,
        ScheduledOffering offering,
        IReadOnlyList<PersonalSchedule> personalSchedules)
    {
        foreach (MeetingSlot slot in offering.MeetingSlots)
        {
            if (node.OccupiedSlots.Contains(slot))
            {
                return false;
            }

            WeeklyTimeRange offeringTimeRange =
                AcademicPeriodTimeTable.GetWeeklyTimeRange(slot);
            foreach (PersonalSchedule personalSchedule in personalSchedules)
            {
                foreach (WeeklyTimeRange personalTimeRange
                    in personalSchedule.TimeRanges)
                {
                    if (ScheduleConflictDetector.HasConflict(
                        offeringTimeRange,
                        personalTimeRange))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
