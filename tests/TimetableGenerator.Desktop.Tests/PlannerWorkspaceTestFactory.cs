using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Tests.Product;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests;

internal static class PlannerWorkspaceTestFactory
{
    public static PlannerWorkspaceViewModel CreateWorkspace()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        IScheduleRecommendationProvider recommendationProvider =
            new CatalogScheduleRecommendationProvider(document.Catalog);
        return createWorkspace(document, recommendationProvider);
    }

    public static PlannerWorkspaceViewModel CreateWorkspace(
        IScheduleRecommendationProvider recommendationProvider)
    {
        if (recommendationProvider == null)
        {
            throw new ArgumentNullException(nameof(recommendationProvider));
        }

        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        return createWorkspace(document, recommendationProvider);
    }

    private static PlannerWorkspaceViewModel createWorkspace(
        CourseCatalogDocument document,
        IScheduleRecommendationProvider recommendationProvider)
    {
        PlanningWorkspace workspace = createPlanningWorkspace(document);
        PlanningWorkspaceSession session = new PlanningWorkspaceSession(
            document.Catalog,
            workspace);
        PlanningWorkspaceAutosaveQueue autosaveQueue =
            new PlanningWorkspaceAutosaveQueue(
                new ImmediatePlanningWorkspaceStore());
        return new PlannerWorkspaceViewModel(
            CourseCatalogProjector.Project(document),
            session,
            autosaveQueue,
            recommendationProvider);
    }

    public static ProductShellViewModel CreateShell(
        PlannerWorkspaceViewModel workspace)
    {
        QueueProductWorkspaceLoader loader = new QueueProductWorkspaceLoader(
            new Func<CancellationToken, Task<PlannerWorkspaceViewModel>>[]
            {
                delegate
                {
                    return Task.FromResult(workspace);
                },
            });
        return new ProductShellViewModel(loader);
    }

    private static PlanningWorkspace createPlanningWorkspace(
        CourseCatalogDocument document)
    {
        PlanCatalogBinding binding = new PlanCatalogBinding(
            document.Catalog.Id,
            document.Catalog.Term,
            document.Catalog.Revision);
        CourseId programmingCourseId = new CourseId("course-programming");
        ScheduledCourseChoice programmingChoice = new ScheduledCourseChoice(
            programmingCourseId,
            new OfferingId[]
            {
                new OfferingId("offering-programming-primary"),
                new OfferingId("offering-programming-alternative"),
            });
        PlanId primaryPlanId = PlanId.CreateNew();
        PlanningPlan primaryPlan = new PlanningPlan(
            primaryPlanId,
            new PlanName("공강 우선"),
            binding,
            new ScheduledCourseChoice[] { programmingChoice },
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningPlan secondaryPlan = new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName("대안 계획"),
            binding,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());
        return new PlanningWorkspace(
            primaryPlanId,
            new PlanningPlan[] { primaryPlan, secondaryPlan });
    }
}
