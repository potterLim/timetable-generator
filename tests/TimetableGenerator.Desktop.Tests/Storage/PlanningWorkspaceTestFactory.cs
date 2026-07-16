using System;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Tests.Storage;

internal static class PlanningWorkspaceTestFactory
{
    public static PlanningWorkspace CreateWorkspace(PlanName planName)
    {
        if (planName == null)
        {
            throw new ArgumentNullException(nameof(planName));
        }

        PlanId planId = PlanId.CreateNew();
        PlanCatalogBinding catalogBinding = new PlanCatalogBinding(
            new CatalogId("handong-global-university:2026-2:r0001"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1));
        PlanningPlan plan = new PlanningPlan(
            planId,
            planName,
            catalogBinding,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());
        return new PlanningWorkspace(planId, new PlanningPlan[] { plan });
    }
}
