using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Domain.Tests.Planning;

[TestClass]
public sealed class CourseChoiceGroupTests
{
    [TestMethod]
    public void CourseCandidateRequiresOneEligibleUniqueOffering()
    {
        CourseId courseId = new CourseId("institution:AAA10001");
        OfferingCandidate preferredCandidate = new OfferingCandidate(
            new OfferingId("institution:term:AAA10001:01"),
            EOfferingPreference.Preferred);
        OfferingCandidate excludedCandidate = new OfferingCandidate(
            new OfferingId("institution:term:AAA10001:02"),
            EOfferingPreference.Excluded);
        List<OfferingCandidate> candidates = new List<OfferingCandidate>
        {
            preferredCandidate,
            excludedCandidate,
        };

        CourseCandidate courseCandidate = new CourseCandidate(courseId, candidates);
        candidates.Clear();

        Assert.HasCount(2, courseCandidate.OfferingCandidates);
        Assert.IsTrue(courseCandidate.OfferingCandidates[0].IsEligible);
        Assert.IsFalse(courseCandidate.OfferingCandidates[1].IsEligible);
        Assert.ThrowsExactly<ArgumentException>(
            () => new CourseCandidate(
                courseId,
                new OfferingCandidate[]
                {
                    excludedCandidate,
                }));
        Assert.ThrowsExactly<ArgumentException>(
            () => new CourseCandidate(
                courseId,
                new OfferingCandidate[]
                {
                    preferredCandidate,
                    new OfferingCandidate(
                        preferredCandidate.OfferingId,
                        EOfferingPreference.Acceptable),
                }));
    }

    [TestMethod]
    public void CourseChoiceGroupRequiresUniqueCoursesAndOfferings()
    {
        CourseCandidate firstCourse = createCourseCandidate(
            "AAA10001",
            "01",
            EOfferingPreference.Preferred);
        CourseCandidate duplicateCourse = createCourseCandidate(
            "AAA10001",
            "02",
            EOfferingPreference.Acceptable);
        CourseCandidate duplicateOffering = new CourseCandidate(
            new CourseId("institution:BBB10001"),
            firstCourse.OfferingCandidates);

        Assert.ThrowsExactly<ArgumentException>(
            () => new CourseChoiceGroup(
                CourseChoiceGroupId.CreateNew(),
                ECourseChoiceCardinality.ExactlyOne,
                new CourseCandidate[] { firstCourse, duplicateCourse }));
        Assert.ThrowsExactly<ArgumentException>(
            () => new CourseChoiceGroup(
                CourseChoiceGroupId.CreateNew(),
                ECourseChoiceCardinality.ExactlyOne,
                new CourseCandidate[] { firstCourse, duplicateOffering }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CourseChoiceGroup(
                CourseChoiceGroupId.CreateNew(),
                (ECourseChoiceCardinality)999,
                new CourseCandidate[] { firstCourse }));
    }

    [TestMethod]
    public void AcceptableOfferingFactoryCreatesASingletonGroup()
    {
        CourseId courseId = new CourseId("institution:AAA10001");
        CourseChoiceGroup group =
            CourseChoiceGroup.CreateWithAcceptableOfferings(
                CourseChoiceGroupId.CreateNew(),
                courseId,
                new OfferingId[]
                {
                    new OfferingId("institution:term:AAA10001:01"),
                    new OfferingId("institution:term:AAA10001:02"),
                });

        Assert.HasCount(1, group.CourseCandidates);
        Assert.HasCount(2, group.CourseCandidates[0].OfferingCandidates);
        Assert.AreEqual(
            EOfferingPreference.Acceptable,
            group.CourseCandidates[0].OfferingCandidates[0].Preference);
        Assert.AreEqual(
            courseId,
            group.CourseCandidates[0].CourseId);
    }

    [TestMethod]
    public void PlanningContentRejectsDuplicateGroupAndCourseIdentities()
    {
        CourseCandidate courseCandidate = createCourseCandidate(
            "AAA10001",
            "01",
            EOfferingPreference.Acceptable);
        CourseChoiceGroup firstGroup = new CourseChoiceGroup(
            CourseChoiceGroupId.CreateNew(),
            ECourseChoiceCardinality.ExactlyOne,
            new CourseCandidate[] { courseCandidate });
        CourseChoiceGroup duplicateCourseGroup = new CourseChoiceGroup(
            CourseChoiceGroupId.CreateNew(),
            ECourseChoiceCardinality.ExactlyOne,
            new CourseCandidate[] { courseCandidate });

        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningPlanContent(
                new CourseChoiceGroup[] { firstGroup, firstGroup },
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningPlanContent(
                new CourseChoiceGroup[] { firstGroup, duplicateCourseGroup },
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
    }

    private static CourseCandidate createCourseCandidate(
        string courseCode,
        string sectionCode,
        EOfferingPreference preference)
    {
        CourseId courseId = new CourseId("institution:" + courseCode);
        OfferingId offeringId = new OfferingId(
            "institution:term:" + courseCode + ":" + sectionCode);
        return new CourseCandidate(
            courseId,
            new OfferingCandidate[]
            {
                new OfferingCandidate(offeringId, preference),
            });
    }
}
