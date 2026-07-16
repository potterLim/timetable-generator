using System.Threading;

using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Recommendations;

internal interface IScheduleRecommendationProvider
{
    ScheduleRecommendationResult Generate(
        PlanningPlan plan,
        ScheduleRecommendationLimit recommendationLimit,
        CancellationToken cancellationToken);
}
