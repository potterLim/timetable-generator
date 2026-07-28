using System;
using System.Threading;
using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Planning;

public sealed class PlanningWorkspaceSession
{
    private readonly CourseCatalog mCatalog;

    private readonly PlanningWorkspaceEditor mEditor;

    private readonly PlanCatalogValidator mPlanCatalogValidator;

    private readonly ScheduleRecommendationGenerator mRecommendationGenerator;

    private PlanningWorkspace mWorkspace;

    public CourseCatalog Catalog
    {
        get
        {
            return mCatalog;
        }
    }

    public PlanningWorkspace Workspace
    {
        get
        {
            return mWorkspace;
        }
    }

    public PlanningWorkspaceSession(CourseCatalog catalog, PlanningWorkspace workspace)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        mCatalog = catalog;
        mEditor = new PlanningWorkspaceEditor();
        mPlanCatalogValidator = new PlanCatalogValidator(catalog);
        mRecommendationGenerator = new ScheduleRecommendationGenerator();
        validateWorkspace(workspace);
        mWorkspace = workspace;
    }

    public PlanningWorkspace ActivatePlan(PlanId planId)
    {
        mWorkspace = mEditor.ActivatePlan(mWorkspace, planId);
        return mWorkspace;
    }

    public PlanningWorkspace AddPlan(PlanId planId, PlanName name)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException("Planning sessions require a valid new plan ID.", nameof(planId));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        PlanCatalogBinding binding = mWorkspace.CatalogBinding;
        PlanningPlan plan = new PlanningPlan(planId, name, binding, new PlanningPlanContent(Array.Empty<CourseChoiceGroup>(), Array.Empty<UnscheduledOfferingSelection>(), Array.Empty<PersonalSchedule>()));
        mWorkspace = mEditor.AddPlan(mWorkspace, plan);
        return mWorkspace;
    }

    public PlanningWorkspace RenamePlan(PlanId planId, PlanName name)
    {
        mWorkspace = mEditor.RenamePlan(mWorkspace, planId, name);
        return mWorkspace;
    }

    public PlanningWorkspace RemovePlan(PlanId planId)
    {
        mWorkspace = mEditor.RemovePlan(mWorkspace, planId);
        return mWorkspace;
    }

    public PlanningWorkspace ClearActivePlanContent()
    {
        mWorkspace = mEditor.ClearPlanContent(mWorkspace, getRequiredActivePlanId());
        return mWorkspace;
    }

    public PlanningWorkspace AddCourse(PlanningCourseSelection selection)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        PlanningWorkspace editedWorkspace;
        switch (selection.Kind)
        {
            case EPlanningCourseSelectionKind.ScheduledAlternatives:
                validateScheduledSelection(selection);
                CourseChoiceGroup courseChoiceGroup = createCourseChoiceGroup(selection);
                editedWorkspace = mEditor.AddCourseChoiceGroup(mWorkspace, getRequiredActivePlanId(), courseChoiceGroup);
                break;
            case EPlanningCourseSelectionKind.TimeNotProvidedOffering:
                UnscheduledOfferingSelection unscheduledSelection = new UnscheduledOfferingSelection(selection.CourseId, selection.GetTimeNotProvidedOfferingId());
                editedWorkspace = mEditor.AddUnscheduledOfferingSelection(mWorkspace, getRequiredActivePlanId(), unscheduledSelection);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(selection), selection.Kind, "Unknown planning course selection kind.");
        }

        validatePlan(editedWorkspace.GetActivePlan());
        mWorkspace = editedWorkspace;
        return mWorkspace;
    }

    public PlanningWorkspace AddCourseChoiceGroup(CourseChoiceGroup courseChoiceGroup)
    {
        if (courseChoiceGroup == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroup));
        }

        PlanningWorkspace editedWorkspace = mEditor.AddCourseChoiceGroup(mWorkspace, getRequiredActivePlanId(), courseChoiceGroup);
        validatePlan(editedWorkspace.GetActivePlan());
        mWorkspace = editedWorkspace;
        return mWorkspace;
    }

    public PlanningWorkspace UpdateCourseChoiceGroup(CourseChoiceGroup courseChoiceGroup)
    {
        if (courseChoiceGroup == null)
        {
            throw new ArgumentNullException(nameof(courseChoiceGroup));
        }

        PlanningWorkspace editedWorkspace = mEditor.UpdateCourseChoiceGroup(mWorkspace, getRequiredActivePlanId(), courseChoiceGroup);
        validatePlan(editedWorkspace.GetActivePlan());
        mWorkspace = editedWorkspace;
        return mWorkspace;
    }

    public PlanningWorkspace RemoveCourseChoiceGroup(CourseChoiceGroupId courseChoiceGroupId)
    {
        mWorkspace = mEditor.RemoveCourseChoiceGroup(mWorkspace, getRequiredActivePlanId(), courseChoiceGroupId);
        return mWorkspace;
    }

    public PlanningWorkspace RemoveCourse(CourseId courseId)
    {
        mWorkspace = mEditor.RemoveCourse(mWorkspace, getRequiredActivePlanId(), courseId);
        return mWorkspace;
    }

    public PlanningWorkspace AddPersonalSchedule(PersonalSchedule personalSchedule)
    {
        if (personalSchedule == null)
        {
            throw new ArgumentNullException(nameof(personalSchedule));
        }

        mWorkspace = mEditor.AddPersonalSchedule(mWorkspace, getRequiredActivePlanId(), personalSchedule);
        return mWorkspace;
    }

    public PlanningWorkspace UpdatePersonalSchedule(PersonalSchedule personalSchedule)
    {
        if (personalSchedule == null)
        {
            throw new ArgumentNullException(nameof(personalSchedule));
        }

        mWorkspace = mEditor.UpdatePersonalSchedule(mWorkspace, getRequiredActivePlanId(), personalSchedule);
        return mWorkspace;
    }

    public PlanningWorkspace RemovePersonalSchedule(PersonalScheduleId personalScheduleId)
    {
        mWorkspace = mEditor.RemovePersonalSchedule(mWorkspace, getRequiredActivePlanId(), personalScheduleId);
        return mWorkspace;
    }

    public PlanningWorkspace RememberLastViewedRecommendation(ScheduleRecommendationBookmark recommendationBookmark)
    {
        if (recommendationBookmark == null)
        {
            throw new ArgumentNullException(nameof(recommendationBookmark));
        }

        mWorkspace = mEditor.RememberLastViewedRecommendation(mWorkspace, getRequiredActivePlanId(), recommendationBookmark);
        return mWorkspace;
    }

    public PlanningWorkspace ForgetLastViewedRecommendation()
    {
        mWorkspace = mEditor.ForgetLastViewedRecommendation(mWorkspace, getRequiredActivePlanId());
        return mWorkspace;
    }

    public ScheduleRecommendationResult GenerateRecommendations(ScheduleRecommendationLimit recommendationLimit, CancellationToken cancellationToken)
    {
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(mCatalog, getRequiredActivePlan(), recommendationLimit);
        return mRecommendationGenerator.GenerateRecommendations(request, cancellationToken);
    }

    private void validateWorkspace(PlanningWorkspace workspace)
    {
        PlanCatalogBinding binding = workspace.CatalogBinding;
        bool doesBindingMatchCatalog = binding.CatalogId == mCatalog.Id
            && binding.InstitutionId == mCatalog.InstitutionId
            && binding.Term == mCatalog.Term
            && binding.Revision == mCatalog.Revision;
        if (doesBindingMatchCatalog == false)
        {
            throw new ArgumentException("The planning workspace must match the session catalog.", nameof(workspace));
        }

        foreach (PlanningPlan plan in workspace.Plans)
        {
            validatePlan(plan);
        }
    }

    private PlanId getRequiredActivePlanId()
    {
        PlanId? activePlanIdOrNull = mWorkspace.ActivePlanIdOrNull;
        if (activePlanIdOrNull.HasValue == false)
        {
            throw new InvalidOperationException("This planning operation requires an active plan.");
        }

        return activePlanIdOrNull.Value;
    }

    private PlanningPlan getRequiredActivePlan()
    {
        return mWorkspace.GetActivePlan();
    }

    private static CourseChoiceGroup createCourseChoiceGroup(PlanningCourseSelection selection)
    {
        return CourseChoiceGroup.CreateWithAcceptableOfferings(CourseChoiceGroupId.CreateNew(), selection.CourseId, selection.GetScheduledOfferingIds());
    }

    private void validateScheduledSelection(PlanningCourseSelection selection)
    {
        foreach (OfferingId offeringId in selection.GetScheduledOfferingIds())
        {
            CatalogOffering? matchingOfferingOrNull = null;
            foreach (CatalogOffering offering in mCatalog.Offerings)
            {
                if (offering.Id == offeringId)
                {
                    matchingOfferingOrNull = offering;
                    break;
                }
            }

            if (matchingOfferingOrNull != null && matchingOfferingOrNull.MeetingSchedule.IsScheduled == false)
            {
                throw new ArgumentException("Scheduled course selections require offerings with provided times.", nameof(selection));
            }
        }
    }

    private void validatePlan(PlanningPlan plan)
    {
        PlanCatalogValidationResult validationResult = mPlanCatalogValidator.Validate(plan);
        if (validationResult.IsValid == false)
        {
            throw new ArgumentException("The planning workspace does not match its course catalog: " + validationResult.Error + ".", nameof(plan));
        }
    }
}
