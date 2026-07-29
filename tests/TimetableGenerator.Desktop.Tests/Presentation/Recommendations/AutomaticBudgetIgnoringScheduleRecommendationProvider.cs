using System;
using System.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests.Presentation.Recommendations;

internal sealed class AutomaticBudgetIgnoringScheduleRecommendationProvider :
    IScheduleRecommendationProvider
{
    private readonly CatalogScheduleRecommendationProvider mInnerProvider;

    public AutomaticBudgetIgnoringScheduleRecommendationProvider(CourseCatalog catalog)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        mInnerProvider = new CatalogScheduleRecommendationProvider(catalog);
    }

    public ScheduleRecommendationResult Generate(
        PlanningPlan plan,
        ScheduleRecommendationLimit recommendationLimit,
        CancellationToken cancellationToken)
    {
        if (recommendationLimit.IsUnlimited == false)
        {
            return mInnerProvider.Generate(plan, recommendationLimit, cancellationToken);
        }

        ScheduleRecommendationResult result = mInnerProvider.Generate(plan, recommendationLimit, CancellationToken.None);
        cancellationToken.WaitHandle.WaitOne();
        return result;
    }
}
