using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Domain.Tests.Planning;

[TestClass]
public sealed class PlanningWorkspaceTests
{
    [TestMethod]
    public void WorkspacePreservesOrderAndReturnsItsActivePlan()
    {
        PlanningPlan firstPlan = createPlan(PlanId.CreateNew(), "공강 중심");
        PlanningPlan secondPlan = createPlan(PlanId.CreateNew(), "전공 중심");

        PlanningWorkspace workspace = new PlanningWorkspace(firstPlan.CatalogBinding, secondPlan.Id, new PlanningPlan[] { firstPlan, secondPlan });

        Assert.AreSame(firstPlan.CatalogBinding, workspace.CatalogBinding);
        Assert.AreEqual(secondPlan.Id, workspace.ActivePlanIdOrNull);
        Assert.IsTrue(workspace.HasPlans);
        Assert.HasCount(2, workspace.Plans);
        Assert.AreSame(firstPlan, workspace.Plans[0]);
        Assert.AreSame(secondPlan, workspace.GetActivePlan());
    }

    [TestMethod]
    public void WorkspaceSupportsAnEmptyStateWithoutAnActivePlan()
    {
        PlanCatalogBinding catalogBinding = createCatalogBinding();

        PlanningWorkspace workspace = new PlanningWorkspace(catalogBinding, null, Array.Empty<PlanningPlan>());

        Assert.AreSame(catalogBinding, workspace.CatalogBinding);
        Assert.IsNull(workspace.ActivePlanIdOrNull);
        Assert.IsFalse(workspace.HasPlans);
        Assert.IsEmpty(workspace.Plans);
        Assert.ThrowsExactly<InvalidOperationException>(workspace.GetActivePlan);
    }

    [TestMethod]
    public void WorkspaceRequiresActivePlanStateToMatchItsPlans()
    {
        PlanningPlan plan = createPlan(PlanId.CreateNew(), "기본 시간표");

        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspace(
                plan.CatalogBinding,
                plan.Id,
                Array.Empty<PlanningPlan>()));
        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspace(
                plan.CatalogBinding,
                null,
                new PlanningPlan[] { plan }));
        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspace(
                plan.CatalogBinding,
                PlanId.CreateNew(),
                new PlanningPlan[] { plan }));
        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspace(
                plan.CatalogBinding,
                default(PlanId),
                new PlanningPlan[] { plan }));
    }

    [TestMethod]
    public void WorkspaceRejectsPlansFromAnotherCatalogBinding()
    {
        PlanCatalogBinding catalogBinding = createCatalogBinding();
        PlanCatalogBinding otherCatalogBinding = createOtherCatalogBinding();
        PlanningPlan plan = createPlan(PlanId.CreateNew(), "기본 시간표", otherCatalogBinding);

        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspace(
                catalogBinding,
                plan.Id,
                new PlanningPlan[] { plan }));
    }

    [TestMethod]
    public void WorkspaceRejectsDuplicateIdsAndCaseInsensitiveNames()
    {
        PlanId sharedPlanId = PlanId.CreateNew();
        PlanningPlan firstPlan = createPlan(sharedPlanId, "기본 시간표");
        PlanningPlan duplicateIdPlan = createPlan(sharedPlanId, "대안 시간표");
        PlanningPlan duplicateNamePlan = createPlan(PlanId.CreateNew(), "기본 시간표");
        PlanningPlan caseVariantNamePlan = createPlan(PlanId.CreateNew(), "BASIC PLAN");
        PlanningPlan englishNamePlan = createPlan(PlanId.CreateNew(), "basic plan");

        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspace(
                firstPlan.CatalogBinding,
                firstPlan.Id,
                new PlanningPlan[] { firstPlan, duplicateIdPlan }));
        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspace(
                firstPlan.CatalogBinding,
                firstPlan.Id,
                new PlanningPlan[] { firstPlan, duplicateNamePlan }));
        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspace(
                englishNamePlan.CatalogBinding,
                englishNamePlan.Id,
                new PlanningPlan[] { englishNamePlan, caseVariantNamePlan }));
    }

    [TestMethod]
    public void WorkspaceDefensivelyCopiesItsPlans()
    {
        PlanningPlan plan = createPlan(PlanId.CreateNew(), "기본 시간표");
        System.Collections.Generic.List<PlanningPlan> mutablePlans = new System.Collections.Generic.List<PlanningPlan>() { plan };

        PlanningWorkspace workspace = new PlanningWorkspace(plan.CatalogBinding, plan.Id, mutablePlans);

        mutablePlans.Clear();

        Assert.HasCount(1, workspace.Plans);
        Assert.AreSame(plan, workspace.GetActivePlan());
    }

    private static PlanningPlan createPlan(PlanId planId, string planName)
    {
        return createPlan(planId, planName, createCatalogBinding());
    }

    private static PlanningPlan createPlan(PlanId planId, string planName, PlanCatalogBinding catalogBinding)
    {
        return new PlanningPlan(planId, new PlanName(planName), catalogBinding, new PlanningPlanContent(Array.Empty<CourseChoiceGroup>(), Array.Empty<UnscheduledOfferingSelection>(), Array.Empty<PersonalSchedule>()));
    }

    private static PlanCatalogBinding createCatalogBinding()
    {
        return new PlanCatalogBinding(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('a', 64)));
    }

    private static PlanCatalogBinding createOtherCatalogBinding()
    {
        return new PlanCatalogBinding(
            new CatalogId("handong-global-university:2026-2:r0002"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(2),
            new CatalogArtifactSha256(new string('b', 64)));
    }
}
