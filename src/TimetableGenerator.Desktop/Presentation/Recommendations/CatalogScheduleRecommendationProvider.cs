using System;
using System.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Recommendations;

internal sealed class CatalogScheduleRecommendationProvider : IScheduleRecommendationProvider
{
    private readonly CourseCatalog mCatalog;

    private readonly ScheduleRecommendationGenerator mGenerator;

    public CatalogScheduleRecommendationProvider(CourseCatalog catalog)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        mCatalog = catalog;
        mGenerator = new ScheduleRecommendationGenerator();
    }

    public ScheduleRecommendationResult Generate(PlanningPlan plan, ScheduleRecommendationLimit recommendationLimit, CancellationToken cancellationToken)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(mCatalog, plan, recommendationLimit);
        return mGenerator.GenerateRecommendations(request, cancellationToken);
    }
}
