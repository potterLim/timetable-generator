using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Scheduling;

internal sealed class PlanCatalogValidator
{
    private readonly CourseCatalog mCatalog;

    private readonly IReadOnlyDictionary<CourseId, CatalogCourse> mCoursesById;

    private readonly IReadOnlyDictionary<OfferingId, CatalogOffering> mOfferingsById;

    public PlanCatalogValidator(CourseCatalog catalog)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        mCatalog = catalog;
        mCoursesById = createCoursesById(catalog);
        mOfferingsById = createOfferingsById(catalog);
    }

    public PlanCatalogValidationResult Validate(PlanningPlan plan)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        EPlanCatalogValidationError bindingError = validateCatalogBinding(
            plan.CatalogBinding);
        if (bindingError != EPlanCatalogValidationError.None)
        {
            return PlanCatalogValidationResult.CreateInvalid(bindingError);
        }

        List<ValidatedCourseChoiceGroup> validatedGroups =
            new List<ValidatedCourseChoiceGroup>();
        foreach (CourseChoiceGroup courseChoiceGroup in plan.CourseChoiceGroups)
        {
            EPlanCatalogValidationError choiceError = validateCourseChoiceGroup(
                courseChoiceGroup,
                validatedGroups);
            if (choiceError != EPlanCatalogValidationError.None)
            {
                return PlanCatalogValidationResult.CreateInvalid(choiceError);
            }
        }

        foreach (UnscheduledOfferingSelection selection
            in plan.UnscheduledOfferingSelections)
        {
            EPlanCatalogValidationError selectionError =
                validateUnscheduledSelection(selection);
            if (selectionError != EPlanCatalogValidationError.None)
            {
                return PlanCatalogValidationResult.CreateInvalid(selectionError);
            }
        }

        return PlanCatalogValidationResult.CreateValid(
            validatedGroups,
            plan.UnscheduledOfferingSelections);
    }

    private EPlanCatalogValidationError validateCatalogBinding(
        PlanCatalogBinding catalogBinding)
    {
        bool hasMatchingCatalogId = mCatalog.Id == catalogBinding.CatalogId;
        bool hasMatchingInstitutionId =
            mCatalog.InstitutionId == catalogBinding.InstitutionId;
        bool hasMatchingTerm = mCatalog.Term == catalogBinding.Term;
        bool hasMatchingRevision = mCatalog.Revision == catalogBinding.Revision;
        if (hasMatchingCatalogId == false
            || hasMatchingInstitutionId == false
            || hasMatchingTerm == false
            || hasMatchingRevision == false)
        {
            return EPlanCatalogValidationError.CatalogBindingMismatch;
        }

        return EPlanCatalogValidationError.None;
    }

    private EPlanCatalogValidationError validateCourseChoiceGroup(
        CourseChoiceGroup courseChoiceGroup,
        ICollection<ValidatedCourseChoiceGroup> validatedGroups)
    {
        List<ValidatedOfferingCandidate> validatedCandidates =
            new List<ValidatedOfferingCandidate>();
        foreach (CourseCandidate courseCandidate
            in courseChoiceGroup.CourseCandidates)
        {
            if (mCoursesById.ContainsKey(courseCandidate.CourseId) == false)
            {
                return EPlanCatalogValidationError.CourseNotFound;
            }

            foreach (OfferingCandidate offeringCandidate
                in courseCandidate.OfferingCandidates)
            {
                CatalogOffering? catalogOfferingOrNull;
                bool hasOffering = mOfferingsById.TryGetValue(
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

                if (offeringCandidate.IsEligible)
                {
                    if (catalogOfferingOrNull.MeetingSchedule.IsScheduled)
                    {
                        validatedCandidates.Add(new ValidatedOfferingCandidate(
                            new ScheduledOffering(catalogOfferingOrNull),
                            offeringCandidate.Preference));
                    }
                    else
                    {
                        validatedCandidates.Add(new ValidatedOfferingCandidate(
                            new UnscheduledOfferingSelection(
                                courseCandidate.CourseId,
                                offeringCandidate.OfferingId),
                            offeringCandidate.Preference));
                    }
                }
            }
        }

        validatedGroups.Add(new ValidatedCourseChoiceGroup(validatedCandidates));
        return EPlanCatalogValidationError.None;
    }

    private EPlanCatalogValidationError validateUnscheduledSelection(
        UnscheduledOfferingSelection selection)
    {
        if (mCoursesById.ContainsKey(selection.CourseId) == false)
        {
            return EPlanCatalogValidationError.CourseNotFound;
        }

        CatalogOffering? catalogOfferingOrNull;
        bool hasOffering = mOfferingsById.TryGetValue(
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

    private static IReadOnlyDictionary<CourseId, CatalogCourse> createCoursesById(
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

    private static IReadOnlyDictionary<OfferingId, CatalogOffering>
        createOfferingsById(CourseCatalog catalog)
    {
        Dictionary<OfferingId, CatalogOffering> offeringsById =
            new Dictionary<OfferingId, CatalogOffering>();
        foreach (CatalogOffering offering in catalog.Offerings)
        {
            offeringsById.Add(offering.Id, offering);
        }

        return offeringsById;
    }
}
