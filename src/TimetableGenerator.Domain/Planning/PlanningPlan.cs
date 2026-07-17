using System;
using System.Collections.Generic;

namespace TimetableGenerator.Domain.Planning;

public sealed class PlanningPlan
{
    public PlanId Id { get; }

    public PlanName Name { get; }

    public PlanCatalogBinding CatalogBinding { get; }

    public PlanningPlanContent Content { get; }

    public ScheduleRecommendationBookmark? LastViewedRecommendationOrNull { get; }

    public IReadOnlyList<CourseChoiceGroup> CourseChoiceGroups
    {
        get
        {
            return Content.CourseChoiceGroups;
        }
    }

    public IReadOnlyList<UnscheduledOfferingSelection> UnscheduledOfferingSelections
    {
        get
        {
            return Content.UnscheduledOfferingSelections;
        }
    }

    public IReadOnlyList<PersonalSchedule> PersonalSchedules
    {
        get
        {
            return Content.PersonalSchedules;
        }
    }

    public bool HasUnscheduledOfferingSelections
    {
        get
        {
            return UnscheduledOfferingSelections.Count > 0;
        }
    }

    public PlanningPlan(
        PlanId id,
        PlanName name,
        PlanCatalogBinding catalogBinding,
        PlanningPlanContent content)
        : this(id, name, catalogBinding, content, null)
    {
    }

    public PlanningPlan(
        PlanId id,
        PlanName name,
        PlanCatalogBinding catalogBinding,
        PlanningPlanContent content,
        ScheduleRecommendationBookmark? lastViewedRecommendationOrNull)
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

        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        Id = id;
        Name = name;
        CatalogBinding = catalogBinding;
        Content = content;
        validateLastViewedRecommendation(content, lastViewedRecommendationOrNull);
        LastViewedRecommendationOrNull = lastViewedRecommendationOrNull;
    }

    private static void validateLastViewedRecommendation(
        PlanningPlanContent content,
        ScheduleRecommendationBookmark? lastViewedRecommendationOrNull)
    {
        if (lastViewedRecommendationOrNull == null)
        {
            return;
        }

        if (lastViewedRecommendationOrNull.ScheduledOfferingIds.Count
            != content.CourseChoiceGroups.Count)
        {
            throw new ArgumentException(
                "The last-viewed recommendation must select one offering per course choice group.",
                nameof(lastViewedRecommendationOrNull));
        }

        foreach (CourseChoiceGroup courseChoiceGroup in content.CourseChoiceGroups)
        {
            if (bookmarkSelectsEligibleOffering(
                lastViewedRecommendationOrNull,
                courseChoiceGroup) == false)
            {
                throw new ArgumentException(
                    "The last-viewed recommendation must reference eligible plan offerings.",
                    nameof(lastViewedRecommendationOrNull));
            }
        }
    }

    private static bool bookmarkSelectsEligibleOffering(
        ScheduleRecommendationBookmark bookmark,
        CourseChoiceGroup courseChoiceGroup)
    {
        foreach (CourseCandidate courseCandidate in courseChoiceGroup.CourseCandidates)
        {
            foreach (OfferingCandidate offeringCandidate
                in courseCandidate.OfferingCandidates)
            {
                if (offeringCandidate.IsEligible
                    && bookmark.ContainsScheduledOffering(
                        offeringCandidate.OfferingId))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
