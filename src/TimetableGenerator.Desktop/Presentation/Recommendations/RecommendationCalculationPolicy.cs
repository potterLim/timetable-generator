using System;

using TimetableGenerator.Application.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Recommendations;

internal sealed class RecommendationCalculationPolicy
{
    private const int INITIAL_RECOMMENDATION_COUNT = 24;

    private static readonly TimeSpan AUTOMATIC_EXHAUSTIVE_CALCULATION_BUDGET = TimeSpan.FromMilliseconds(750.0);

    public static RecommendationCalculationPolicy Default { get; } = new RecommendationCalculationPolicy(new ScheduleRecommendationLimit(INITIAL_RECOMMENDATION_COUNT), AUTOMATIC_EXHAUSTIVE_CALCULATION_BUDGET);

    public ScheduleRecommendationLimit InitialRecommendationLimit { get; }

    public TimeSpan AutomaticExhaustiveCalculationBudget { get; }

    public RecommendationCalculationPolicy(ScheduleRecommendationLimit initialRecommendationLimit, TimeSpan automaticExhaustiveCalculationBudget)
    {
        if (initialRecommendationLimit.IsValid == false || initialRecommendationLimit.IsUnlimited)
        {
            throw new ArgumentOutOfRangeException(nameof(initialRecommendationLimit), initialRecommendationLimit, "The initial recommendation limit must be finite and positive.");
        }

        if (automaticExhaustiveCalculationBudget < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(automaticExhaustiveCalculationBudget), automaticExhaustiveCalculationBudget, "The automatic exhaustive calculation budget cannot be negative.");
        }

        InitialRecommendationLimit = initialRecommendationLimit;
        AutomaticExhaustiveCalculationBudget = automaticExhaustiveCalculationBudget;
    }
}
