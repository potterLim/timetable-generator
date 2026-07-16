using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Domain.Tests.Planning;

[TestClass]
public sealed class PlanningPlanTests
{
    [TestMethod]
    public void PlanNameNormalizesAndRejectsAmbiguousDisplayValues()
    {
        PlanName planName = new PlanName("  공강 중심  ");

        Assert.AreEqual("공강 중심", planName.Value);
        Assert.ThrowsExactly<ArgumentException>(() => new PlanName("  "));
        Assert.ThrowsExactly<ArgumentException>(() => new PlanName("첫째 줄\n둘째 줄"));
        Assert.ThrowsExactly<ArgumentException>(() => new PlanName(new string('가', 81)));
    }

    [TestMethod]
    public void ScheduledCourseChoiceRequiresUniqueOfferingsAndDefensivelyCopiesThem()
    {
        CourseId courseId = createCourseId("CSE30001");
        OfferingId firstOfferingId = createOfferingId("CSE30001", "01");
        List<OfferingId> mutableOfferingIds = new List<OfferingId>()
        {
            firstOfferingId,
        };
        ScheduledCourseChoice choice = new ScheduledCourseChoice(
            courseId,
            mutableOfferingIds);

        mutableOfferingIds.Add(createOfferingId("CSE30001", "02"));

        Assert.HasCount(1, choice.OfferingIds);
        Assert.ThrowsExactly<ArgumentException>(
            () => new ScheduledCourseChoice(courseId, Array.Empty<OfferingId>()));
        Assert.ThrowsExactly<ArgumentException>(
            () => new ScheduledCourseChoice(
                courseId,
                new OfferingId[] { firstOfferingId, firstOfferingId }));
    }

    [TestMethod]
    public void PlanKeepsUnscheduledSelectionsOutsideScheduledChoices()
    {
        ScheduledCourseChoice scheduledChoice = new ScheduledCourseChoice(
            createCourseId("CSE30001"),
            new OfferingId[] { createOfferingId("CSE30001", "01") });
        UnscheduledOfferingSelection unscheduledSelection =
            new UnscheduledOfferingSelection(
                createCourseId("CSE30002"),
                createOfferingId("CSE30002", "01"));

        PlanningPlan plan = createPlan(
            PlanId.CreateNew(),
            "기본 시간표",
            new ScheduledCourseChoice[] { scheduledChoice },
            new UnscheduledOfferingSelection[] { unscheduledSelection });

        Assert.HasCount(1, plan.ScheduledCourseChoices);
        Assert.HasCount(1, plan.UnscheduledOfferingSelections);
        Assert.IsTrue(plan.HasUnscheduledOfferingSelections);
        Assert.AreEqual(
            unscheduledSelection.OfferingId,
            plan.UnscheduledOfferingSelections[0].OfferingId);
    }

    [TestMethod]
    public void PlanRejectsDuplicateAndMixedCourseSelections()
    {
        CourseId courseId = createCourseId("CSE30001");
        ScheduledCourseChoice firstChoice = new ScheduledCourseChoice(
            courseId,
            new OfferingId[] { createOfferingId("CSE30001", "01") });
        ScheduledCourseChoice duplicateCourseChoice = new ScheduledCourseChoice(
            courseId,
            new OfferingId[] { createOfferingId("CSE30001", "02") });
        UnscheduledOfferingSelection mixedSelection = new UnscheduledOfferingSelection(
            courseId,
            createOfferingId("CSE30001", "03"));

        Assert.ThrowsExactly<ArgumentException>(
            () => createPlan(
                PlanId.CreateNew(),
                "중복 과목",
                new ScheduledCourseChoice[] { firstChoice, duplicateCourseChoice },
                Array.Empty<UnscheduledOfferingSelection>()));
        Assert.ThrowsExactly<ArgumentException>(
            () => createPlan(
                PlanId.CreateNew(),
                "혼합 과목",
                new ScheduledCourseChoice[] { firstChoice },
                new UnscheduledOfferingSelection[] { mixedSelection }));
    }

    [TestMethod]
    public void EmptyNewPlanIsAValidNamedWorkspaceDocument()
    {
        PlanningPlan plan = createPlan(
            PlanId.CreateNew(),
            "새 시간표",
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());

        Assert.IsEmpty(plan.ScheduledCourseChoices);
        Assert.IsEmpty(plan.UnscheduledOfferingSelections);
        Assert.IsFalse(plan.HasUnscheduledOfferingSelections);
    }

    private static PlanningPlan createPlan(
        PlanId planId,
        string planName,
        IEnumerable<ScheduledCourseChoice> scheduledCourseChoices,
        IEnumerable<UnscheduledOfferingSelection> unscheduledOfferingSelections)
    {
        return new PlanningPlan(
            planId,
            new PlanName(planName),
            createCatalogBinding(),
            scheduledCourseChoices,
            unscheduledOfferingSelections);
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

    private static CourseId createCourseId(string courseCodeValue)
    {
        return new CourseId("handong-global-university:" + courseCodeValue);
    }

    private static OfferingId createOfferingId(
        string courseCodeValue,
        string sectionCodeValue)
    {
        return new OfferingId(
            "handong-global-university:2026-2:"
            + courseCodeValue
            + ":"
            + sectionCodeValue);
    }
}
