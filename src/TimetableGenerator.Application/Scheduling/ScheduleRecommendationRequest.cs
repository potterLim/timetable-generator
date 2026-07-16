using System;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Scheduling;

public sealed class ScheduleRecommendationRequest
{
    public CourseCatalog Catalog { get; }

    public PlanningPlan Plan { get; }

    public ScheduleRecommendationLimit MaximumRecommendationCount { get; }

    public ScheduleRecommendationRequest(
        CourseCatalog catalog,
        PlanningPlan plan,
        ScheduleRecommendationLimit maximumRecommendationCount)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (maximumRecommendationCount.IsValid == false)
        {
            throw new ArgumentException(
                "Schedule recommendation requests require a valid result limit.",
                nameof(maximumRecommendationCount));
        }

        Catalog = catalog;
        Plan = plan;
        MaximumRecommendationCount = maximumRecommendationCount;
    }
}
