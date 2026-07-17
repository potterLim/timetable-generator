using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Tests.Scheduling;

[TestClass]
public sealed class CourseChoiceRecommendationTests
{
    [TestMethod]
    public void GroupChoosesOneCourseAndOneEligibleOffering()
    {
        CatalogCourse firstCourse =
            ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse secondCourse =
            ScheduleRecommendationTestData.CreateCourse("BBB10001");
        CatalogOffering preferredOffering = createOffering(
            "AAA10001",
            "01",
            EDay.Monday,
            1);
        CatalogOffering excludedOffering = createOffering(
            "AAA10001",
            "02",
            EDay.Tuesday,
            1);
        CatalogOffering acceptableOffering = createOffering(
            "BBB10001",
            "01",
            EDay.Wednesday,
            1);
        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { firstCourse, secondCourse },
            new CatalogOffering[]
            {
                preferredOffering,
                excludedOffering,
                acceptableOffering,
            });
        CourseCandidate firstCandidate = new CourseCandidate(
            firstCourse.Id,
            new OfferingCandidate[]
            {
                new OfferingCandidate(
                    preferredOffering.Id,
                    EOfferingPreference.Preferred),
                new OfferingCandidate(
                    excludedOffering.Id,
                    EOfferingPreference.Excluded),
            });
        CourseCandidate secondCandidate = new CourseCandidate(
            secondCourse.Id,
            new OfferingCandidate[]
            {
                new OfferingCandidate(
                    acceptableOffering.Id,
                    EOfferingPreference.Acceptable),
            });
        CourseChoiceGroup courseChoiceGroup = new CourseChoiceGroup(
            CourseChoiceGroupId.CreateNew(),
            ECourseChoiceCardinality.ExactlyOne,
            new CourseCandidate[] { firstCandidate, secondCandidate });
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            new CourseChoiceGroup[] { courseChoiceGroup },
            Array.Empty<UnscheduledOfferingSelection>());

        ScheduleRecommendationResult result = generate(catalog, plan, 10);

        Assert.HasCount(2, result.Recommendations);
        Assert.HasCount(1, result.Recommendations[0].ScheduledOfferings);
        Assert.HasCount(1, result.Recommendations[1].ScheduledOfferings);
        Assert.AreEqual(preferredOffering.Id, getOfferingId(result, 0));
        Assert.AreEqual(RecommendationScore.ZERO, result.Recommendations[0].Score);
        Assert.AreEqual(acceptableOffering.Id, getOfferingId(result, 1));
        Assert.AreEqual(new RecommendationScore(1), result.Recommendations[1].Score);
    }

    [TestMethod]
    public void LimitKeepsGloballyBestScoresInsteadOfDepthFirstPrefix()
    {
        List<CatalogCourse> courses = new List<CatalogCourse>();
        List<CatalogOffering> offerings = new List<CatalogOffering>();
        List<CourseChoiceGroup> groups = new List<CourseChoiceGroup>();
        string[] courseCodes = new string[]
        {
            "AAA10001",
            "BBB10001",
            "CCC10001",
        };
        EDay[] courseDays = new EDay[]
        {
            EDay.Monday,
            EDay.Tuesday,
            EDay.Wednesday,
        };
        for (int courseIndex = 0;
            courseIndex < courseCodes.Length;
            ++courseIndex)
        {
            string courseCode = courseCodes[courseIndex];
            CatalogCourse course = ScheduleRecommendationTestData.CreateCourse(
                courseCode);
            CatalogOffering preferredOffering = createOffering(
                courseCode,
                "01",
                courseDays[courseIndex],
                1);
            CatalogOffering acceptableOffering = createOffering(
                courseCode,
                "02",
                courseDays[courseIndex],
                2);
            courses.Add(course);
            offerings.Add(preferredOffering);
            offerings.Add(acceptableOffering);
            CourseCandidate courseCandidate = new CourseCandidate(
                course.Id,
                new OfferingCandidate[]
                {
                    new OfferingCandidate(
                        preferredOffering.Id,
                        EOfferingPreference.Preferred),
                    new OfferingCandidate(
                        acceptableOffering.Id,
                        EOfferingPreference.Acceptable),
                });
            groups.Add(new CourseChoiceGroup(
                CourseChoiceGroupId.CreateNew(),
                ECourseChoiceCardinality.ExactlyOne,
                new CourseCandidate[] { courseCandidate }));
        }

        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            courses,
            offerings);
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            groups,
            Array.Empty<UnscheduledOfferingSelection>());

        ScheduleRecommendationResult result = generate(catalog, plan, 4);

        CollectionAssert.AreEqual(
            new int[] { 0, 1, 1, 1 },
            getScoreValues(result));
        Assert.AreEqual(
            EScheduleRecommendationCompletion.MaximumRecommendationCountReached,
            result.Completion);
    }

    [TestMethod]
    public void RecommendationScoreRejectsNegativeValuesAndAddsStrongly()
    {
        RecommendationScore firstScore = new RecommendationScore(2);
        RecommendationScore secondScore = new RecommendationScore(3);

        RecommendationScore combinedScore = firstScore.Add(secondScore);

        Assert.AreEqual(new RecommendationScore(5), combinedScore);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new RecommendationScore(-1));
    }

    private static CatalogOffering createOffering(
        string courseCode,
        string sectionCode,
        EDay day,
        int period)
    {
        return ScheduleRecommendationTestData.CreateScheduledOffering(
            courseCode,
            sectionCode,
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(day, period),
            });
    }

    private static ScheduleRecommendationResult generate(
        CourseCatalog catalog,
        PlanningPlan plan,
        int limit)
    {
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(
            catalog,
            plan,
            new ScheduleRecommendationLimit(limit));
        ScheduleRecommendationGenerator generator = new ScheduleRecommendationGenerator();
        return generator.GenerateRecommendations(request, CancellationToken.None);
    }

    private static OfferingId getOfferingId(
        ScheduleRecommendationResult result,
        int recommendationIndex)
    {
        return result.Recommendations[recommendationIndex]
            .ScheduledOfferings[0]
            .OfferingId;
    }

    private static int[] getScoreValues(ScheduleRecommendationResult result)
    {
        List<int> scoreValues = new List<int>();
        foreach (ScheduleRecommendation recommendation in result.Recommendations)
        {
            scoreValues.Add(recommendation.Score.Value);
        }

        return scoreValues.ToArray();
    }
}
