using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Application.Tests.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Application.Tests.Planning;

[TestClass]
public sealed class PlanningWorkspaceEditorTests
{
    [TestMethod]
    public void ActivatePlanPreservesEveryPlan()
    {
        PlanningPlan firstPlan = createPlan("첫 계획");
        PlanningPlan secondPlan = createPlan("둘째 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            firstPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();

        PlanningWorkspace result = editor.ActivatePlan(workspace, secondPlan.Id);

        Assert.AreEqual(secondPlan.Id, result.ActivePlanId);
        Assert.HasCount(2, result.Plans);
        Assert.AreSame(firstPlan, result.Plans[0]);
        Assert.AreSame(secondPlan, result.Plans[1]);
    }

    [TestMethod]
    public void AddPlanMakesTheAddedPlanActive()
    {
        PlanningPlan existingPlan = createPlan("기본 계획");
        PlanningPlan addedPlan = createPlan("새 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            existingPlan.Id,
            new PlanningPlan[] { existingPlan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();

        PlanningWorkspace result = editor.AddPlan(workspace, addedPlan);

        Assert.AreEqual(addedPlan.Id, result.ActivePlanId);
        Assert.HasCount(2, result.Plans);
        Assert.AreSame(addedPlan, result.Plans[1]);
    }

    [TestMethod]
    public void RenamePlanPreservesItsCatalogAndChoices()
    {
        ScheduledCourseChoice choice = ScheduleRecommendationTestData.CreateChoice(
            "AAA10001",
            "01");
        PlanningPlan plan = createPlan(
            "변경 전",
            new ScheduledCourseChoice[] { choice },
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();

        PlanningWorkspace result = editor.RenamePlan(
            workspace,
            plan.Id,
            new PlanName("변경 후"));

        PlanningPlan renamedPlan = result.GetActivePlan();
        Assert.AreEqual("변경 후", renamedPlan.Name.Value);
        Assert.AreSame(plan.CatalogBinding, renamedPlan.CatalogBinding);
        Assert.AreSame(choice, renamedPlan.ScheduledCourseChoices[0]);
    }

    [TestMethod]
    public void AddScheduledChoiceUpdatesOnlyTheRequestedPlan()
    {
        PlanningPlan firstPlan = createPlan("첫 계획");
        PlanningPlan secondPlan = createPlan("둘째 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            firstPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();
        ScheduledCourseChoice addedChoice = ScheduleRecommendationTestData.CreateChoice(
            "AAA10001",
            "01",
            "02");

        PlanningWorkspace result = editor.AddScheduledCourseChoice(
            workspace,
            firstPlan.Id,
            addedChoice);

        Assert.HasCount(1, result.Plans[0].ScheduledCourseChoices);
        Assert.AreSame(addedChoice, result.Plans[0].ScheduledCourseChoices[0]);
        Assert.AreSame(secondPlan, result.Plans[1]);
    }

    [TestMethod]
    public void AddUnscheduledSelectionKeepsManualReviewDataSeparate()
    {
        PlanningPlan plan = createPlan("기본 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();
        UnscheduledOfferingSelection selection =
            ScheduleRecommendationTestData.CreateUnscheduledSelection(
                "AAA10001",
                "01");

        PlanningWorkspace result = editor.AddUnscheduledOfferingSelection(
            workspace,
            plan.Id,
            selection);

        PlanningPlan updatedPlan = result.GetActivePlan();
        Assert.IsEmpty(updatedPlan.ScheduledCourseChoices);
        Assert.HasCount(1, updatedPlan.UnscheduledOfferingSelections);
        Assert.AreSame(selection, updatedPlan.UnscheduledOfferingSelections[0]);
    }

    [TestMethod]
    public void RemoveCourseRemovesEitherScheduleStatus()
    {
        ScheduledCourseChoice scheduledChoice =
            ScheduleRecommendationTestData.CreateChoice("AAA10001", "01");
        UnscheduledOfferingSelection unscheduledSelection =
            ScheduleRecommendationTestData.CreateUnscheduledSelection(
                "BBB10001",
                "01");
        PlanningPlan plan = createPlan(
            "기본 계획",
            new ScheduledCourseChoice[] { scheduledChoice },
            new UnscheduledOfferingSelection[] { unscheduledSelection });
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();

        PlanningWorkspace withoutScheduled = editor.RemoveCourse(
            workspace,
            plan.Id,
            scheduledChoice.CourseId);
        PlanningWorkspace withoutEither = editor.RemoveCourse(
            withoutScheduled,
            plan.Id,
            unscheduledSelection.CourseId);

        Assert.IsEmpty(withoutEither.GetActivePlan().ScheduledCourseChoices);
        Assert.IsEmpty(withoutEither.GetActivePlan().UnscheduledOfferingSelections);
    }

    [TestMethod]
    public void RemoveActivePlanSelectsItsNearestRemainingNeighbor()
    {
        PlanningPlan firstPlan = createPlan("첫 계획");
        PlanningPlan secondPlan = createPlan("둘째 계획");
        PlanningPlan thirdPlan = createPlan("셋째 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            secondPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan, thirdPlan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();

        PlanningWorkspace result = editor.RemovePlan(workspace, secondPlan.Id);

        Assert.AreEqual(thirdPlan.Id, result.ActivePlanId);
        Assert.HasCount(2, result.Plans);
        Assert.AreSame(firstPlan, result.Plans[0]);
        Assert.AreSame(thirdPlan, result.Plans[1]);
    }

    [TestMethod]
    public void RemovePlanRejectsDeletingTheFinalPlan()
    {
        PlanningPlan plan = createPlan("기본 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => editor.RemovePlan(workspace, plan.Id));
    }

    private static PlanningPlan createPlan(string name)
    {
        return createPlan(
            name,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());
    }

    private static PlanningPlan createPlan(
        string name,
        ScheduledCourseChoice[] scheduledChoices,
        UnscheduledOfferingSelection[] unscheduledSelections)
    {
        PlanCatalogBinding binding = new PlanCatalogBinding(
            new CatalogId("handong-global-university:2026-2:r0001"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1));
        return new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName(name),
            binding,
            scheduledChoices,
            unscheduledSelections);
    }
}
