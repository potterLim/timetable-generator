using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Recommendations;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Product;
using TimetableGenerator.Desktop.Product.CatalogUpdates;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Desktop.Storage;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Tests.Product;
using TimetableGenerator.Desktop.Tests.Product.Loading;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Catalogs;

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

    public static PlannerWorkspaceViewModel CreateWorkspace(
        CourseCatalogDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        IScheduleRecommendationProvider recommendationProvider =
            new CatalogScheduleRecommendationProvider(document.Catalog);
        return createWorkspace(document, recommendationProvider);
    }

    public static ProductShellViewModel CreateShell(
        PlannerWorkspaceViewModel workspace)
    {
        QueueProductWorkspaceLoader loader = new QueueProductWorkspaceLoader(
            new Func<CancellationToken, Task<ProductWorkspacePresentation>>[]
            {
                delegate
                {
                    return Task.FromResult(CreatePresentation(workspace));
                },
            });
        QueueProductCatalogUpdateService catalogUpdateService =
            new QueueProductCatalogUpdateService(
                new Func<
                    VerifiedCatalogPackage,
                    PlanningWorkspace,
                    CancellationToken,
                    Task<ProductCatalogUpdateResult>>[]
                {
                    delegate
                    {
                        ProductCatalogUpdateResult updateResult =
                            new ProductCatalogUpdateResult(
                                EProductCatalogUpdateStatus.Current,
                                new CatalogRevision(1));
                        return Task.FromResult(updateResult);
                    },
                });
        return new ProductShellViewModel(loader, catalogUpdateService);
    }

    public static ProductWorkspacePresentation CreatePresentation(
        PlannerWorkspaceViewModel workspace)
    {
        return CreatePresentation(
            workspace,
            EProductCatalogOrigin.OfflineCache);
    }

    public static ProductWorkspacePresentation CreatePresentation(
        PlannerWorkspaceViewModel workspace,
        EProductCatalogOrigin catalogOrigin)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage activeCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(revision);
        PlanningWorkspace workspaceSnapshot =
            ProductWorkspaceLoaderTestData.CreateEmptyWorkspace(revision);
        return new ProductWorkspacePresentation(
            workspace,
            activeCatalogPackage,
            workspaceSnapshot,
            catalogOrigin,
            EProductWorkspaceRecoveryFlags.None);
    }

    public static ProductWorkspacePresentation CreatePresentationWithRecoveryFlags(
        PlannerWorkspaceViewModel workspace,
        EProductWorkspaceRecoveryFlags recoveryFlags)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        CatalogRevision revision = new CatalogRevision(1);
        VerifiedCatalogPackage activeCatalogPackage =
            ProductWorkspaceLoaderTestData.CreateCatalogPackage(revision);
        PlanningWorkspace workspaceSnapshot =
            ProductWorkspaceLoaderTestData.CreateEmptyWorkspace(revision);
        return new ProductWorkspacePresentation(
            workspace,
            activeCatalogPackage,
            workspaceSnapshot,
            EProductCatalogOrigin.OfflineCache,
            recoveryFlags);
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

    private static PlanningWorkspace createPlanningWorkspace(
        CourseCatalogDocument document)
    {
        PlanCatalogBinding binding = new PlanCatalogBinding(
            document.Catalog.Id,
            document.Catalog.InstitutionId,
            document.Catalog.Term,
            document.Catalog.Revision,
            new CatalogArtifactSha256(new string('a', 64)));
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
            new PlanningPlanContent(
                new ScheduledCourseChoice[] { programmingChoice },
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        PlanningPlan secondaryPlan = new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName("대안 계획"),
            binding,
            new PlanningPlanContent(
                Array.Empty<ScheduledCourseChoice>(),
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        return new PlanningWorkspace(
            primaryPlanId,
            new PlanningPlan[] { primaryPlan, secondaryPlan });
    }
}
