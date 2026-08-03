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
        Assert.ThrowsExactly<ArgumentException>(() => new PlanName(new string('가', PlanName.MAXIMUM_LENGTH + 1)));
    }

    [TestMethod]
    public void CourseChoiceGroupRequiresUniqueOfferingsAndDefensivelyCopiesThem()
    {
        CourseId courseId = createCourseId("CSE30001");
        OfferingId firstOfferingId = createOfferingId("CSE30001", "01");
        List<OfferingCandidate> mutableOfferingCandidates = new List<OfferingCandidate>()
        {
            new OfferingCandidate(
                firstOfferingId,
                EOfferingPreference.Acceptable),
        };
        CourseCandidate courseCandidate = new CourseCandidate(courseId, mutableOfferingCandidates);

        mutableOfferingCandidates.Add(new OfferingCandidate(createOfferingId("CSE30001", "02"), EOfferingPreference.Acceptable));

        Assert.HasCount(1, courseCandidate.OfferingCandidates);
        Assert.ThrowsExactly<ArgumentException>(() => new CourseCandidate(courseId, Array.Empty<OfferingCandidate>()));
        Assert.ThrowsExactly<ArgumentException>(
            () => new CourseCandidate(
                courseId,
                new OfferingCandidate[]
                {
                    new OfferingCandidate(
                        firstOfferingId,
                        EOfferingPreference.Acceptable),
                    new OfferingCandidate(
                        firstOfferingId,
                        EOfferingPreference.Preferred),
                }));
    }

    [TestMethod]
    public void PlanKeepsUnscheduledSelectionsOutsideCourseChoiceGroups()
    {
        CourseChoiceGroup courseChoiceGroup = createCourseChoiceGroup("CSE30001", "01");
        UnscheduledOfferingSelection unscheduledSelection = new UnscheduledOfferingSelection(createCourseId("CSE30002"), createOfferingId("CSE30002", "01"));

        PlanningPlan plan = createPlan(PlanId.CreateNew(), "기본 시간표", new CourseChoiceGroup[] { courseChoiceGroup }, new UnscheduledOfferingSelection[] { unscheduledSelection });

        Assert.HasCount(1, plan.CourseChoiceGroups);
        Assert.HasCount(1, plan.UnscheduledOfferingSelections);
        Assert.IsTrue(plan.HasUnscheduledOfferingSelections);
        Assert.AreEqual(unscheduledSelection.OfferingId, plan.UnscheduledOfferingSelections[0].OfferingId);
    }

    [TestMethod]
    public void PlanRejectsDuplicateAndMixedCourseSelections()
    {
        CourseId courseId = createCourseId("CSE30001");
        CourseChoiceGroup firstChoiceGroup = createCourseChoiceGroup("CSE30001", "01");
        CourseChoiceGroup duplicateCourseChoiceGroup = createCourseChoiceGroup("CSE30001", "02");
        UnscheduledOfferingSelection mixedSelection = new UnscheduledOfferingSelection(courseId, createOfferingId("CSE30001", "03"));

        Assert.ThrowsExactly<ArgumentException>(
            () => createPlan(
                PlanId.CreateNew(),
                "중복 과목",
                new CourseChoiceGroup[]
                {
                    firstChoiceGroup,
                    duplicateCourseChoiceGroup,
                },
                Array.Empty<UnscheduledOfferingSelection>()));
        Assert.ThrowsExactly<ArgumentException>(
            () => createPlan(
                PlanId.CreateNew(),
                "혼합 과목",
                new CourseChoiceGroup[] { firstChoiceGroup },
                new UnscheduledOfferingSelection[] { mixedSelection }));
    }

    [TestMethod]
    public void EmptyNewPlanIsAValidNamedWorkspaceDocument()
    {
        PlanningPlan plan = createPlan(PlanId.CreateNew(), "새 시간표", Array.Empty<CourseChoiceGroup>(), Array.Empty<UnscheduledOfferingSelection>());

        Assert.IsEmpty(plan.CourseChoiceGroups);
        Assert.IsEmpty(plan.UnscheduledOfferingSelections);
        Assert.IsFalse(plan.HasUnscheduledOfferingSelections);
    }

    private static PlanningPlan createPlan(PlanId planId, string planName, IEnumerable<CourseChoiceGroup> courseChoiceGroups, IEnumerable<UnscheduledOfferingSelection> unscheduledOfferingSelections)
    {
        return new PlanningPlan(planId, new PlanName(planName), createCatalogBinding(), new PlanningPlanContent(courseChoiceGroups, unscheduledOfferingSelections, Array.Empty<PersonalSchedule>()));
    }

    private static CourseChoiceGroup createCourseChoiceGroup(string courseCodeValue, string sectionCodeValue)
    {
        CourseId courseId = createCourseId(courseCodeValue);
        OfferingCandidate offeringCandidate = new OfferingCandidate(createOfferingId(courseCodeValue, sectionCodeValue), EOfferingPreference.Acceptable);
        CourseCandidate courseCandidate = new CourseCandidate(courseId, new OfferingCandidate[] { offeringCandidate });
        return new CourseChoiceGroup(CourseChoiceGroupId.CreateNew(), ECourseChoiceCardinality.ExactlyOne, new CourseCandidate[] { courseCandidate });
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

    private static OfferingId createOfferingId(string courseCodeValue, string sectionCodeValue)
    {
        return new OfferingId("handong-global-university:2026-2:" + courseCodeValue + ":" + sectionCodeValue);
    }
}
