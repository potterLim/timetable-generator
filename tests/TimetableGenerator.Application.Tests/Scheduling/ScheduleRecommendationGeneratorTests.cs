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
public sealed class ScheduleRecommendationGeneratorTests
{
    [TestMethod]
    public void GenerateRecommendationsPreservesChoiceAndOfferingOrderDeterministically()
    {
        CourseCatalog catalog = createCartesianCatalog();
        PlanningPlan plan = createCartesianPlan(catalog);
        ScheduleRecommendationGenerator generator = new ScheduleRecommendationGenerator();
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(
            catalog,
            plan,
            new ScheduleRecommendationLimit(10));

        ScheduleRecommendationResult firstResult = generator.GenerateRecommendations(
            request,
            CancellationToken.None);
        ScheduleRecommendationResult secondResult = generator.GenerateRecommendations(
            request,
            CancellationToken.None);

        string[] expectedOrder = new string[]
        {
            "AAA10001:01,BBB10001:01",
            "AAA10001:01,BBB10001:02",
            "AAA10001:02,BBB10001:01",
            "AAA10001:02,BBB10001:02",
        };
        CollectionAssert.AreEqual(expectedOrder, getRecommendationNames(firstResult));
        CollectionAssert.AreEqual(
            getRecommendationNames(firstResult),
            getRecommendationNames(secondResult));
        Assert.AreEqual(EScheduleRecommendationCompletion.Completed, firstResult.Completion);
        foreach (ScheduleRecommendation recommendation in firstResult.Recommendations)
        {
            Assert.HasCount(2, recommendation.ScheduledOfferings);
            Assert.AreEqual(
                ERecommendationVerificationStatus.ConfirmedConflictFree,
                recommendation.VerificationStatus);
        }
    }

    [TestMethod]
    public void GenerateRecommendationsPrunesOnlyConflictingBranches()
    {
        CatalogCourse firstCourse = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse secondCourse = ScheduleRecommendationTestData.CreateCourse("BBB10001");
        CatalogOffering firstMonday = ScheduleRecommendationTestData.CreateScheduledOffering(
            "AAA10001",
            "01",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Monday, 1),
            });
        CatalogOffering firstTuesday = ScheduleRecommendationTestData.CreateScheduledOffering(
            "AAA10001",
            "02",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Tuesday, 1),
            });
        CatalogOffering secondMonday = ScheduleRecommendationTestData.CreateScheduledOffering(
            "BBB10001",
            "01",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Monday, 1),
            });
        CatalogOffering secondWednesday = ScheduleRecommendationTestData.CreateScheduledOffering(
            "BBB10001",
            "02",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Wednesday, 1),
            });
        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { firstCourse, secondCourse },
            new CatalogOffering[]
            {
                firstMonday,
                firstTuesday,
                secondMonday,
                secondWednesday,
            });
        PlanningPlan plan = createCartesianPlan(catalog);

        ScheduleRecommendationResult result = generate(catalog, plan, 10);

        CollectionAssert.AreEqual(
            new string[]
            {
                "AAA10001:01,BBB10001:02",
                "AAA10001:02,BBB10001:01",
                "AAA10001:02,BBB10001:02",
            },
            getRecommendationNames(result));
    }

    [TestMethod]
    public void GenerateRecommendationsReturnsNoResultWhenChoicesCannotBeSatisfied()
    {
        CatalogCourse firstCourse = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse secondCourse = ScheduleRecommendationTestData.CreateCourse("BBB10001");
        MeetingSlot occupiedSlot = ScheduleRecommendationTestData.CreateMeetingSlot(
            EDay.Monday,
            1);
        CatalogOffering firstOffering = ScheduleRecommendationTestData.CreateScheduledOffering(
            "AAA10001",
            "01",
            new MeetingSlot[] { occupiedSlot });
        CatalogOffering secondOffering = ScheduleRecommendationTestData.CreateScheduledOffering(
            "BBB10001",
            "01",
            new MeetingSlot[] { occupiedSlot });
        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { firstCourse, secondCourse },
            new CatalogOffering[] { firstOffering, secondOffering });
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            new ScheduledCourseChoice[]
            {
                ScheduleRecommendationTestData.CreateChoice("AAA10001", "01"),
                ScheduleRecommendationTestData.CreateChoice("BBB10001", "01"),
            },
            Array.Empty<UnscheduledOfferingSelection>());

        ScheduleRecommendationResult result = generate(catalog, plan, 10);

        Assert.AreEqual(EScheduleRecommendationCompletion.Completed, result.Completion);
        Assert.IsEmpty(result.Recommendations);
    }

    [TestMethod]
    public void GenerateRecommendationsReportsLimitOnlyWhenAnotherResultExists()
    {
        CourseCatalog catalog = createCartesianCatalog();
        PlanningPlan plan = createCartesianPlan(catalog);

        ScheduleRecommendationResult limitedResult = generate(catalog, plan, 2);
        ScheduleRecommendationResult exactResult = generate(catalog, plan, 4);

        Assert.HasCount(2, limitedResult.Recommendations);
        Assert.AreEqual(
            EScheduleRecommendationCompletion.MaximumRecommendationCountReached,
            limitedResult.Completion);
        Assert.HasCount(4, exactResult.Recommendations);
        Assert.AreEqual(EScheduleRecommendationCompletion.Completed, exactResult.Completion);
    }

    [TestMethod]
    public void GenerateRecommendationsReturnsTypedCancellation()
    {
        CourseCatalog catalog = createCartesianCatalog();
        PlanningPlan plan = createCartesianPlan(catalog);
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(
            catalog,
            plan,
            new ScheduleRecommendationLimit(10));
        ScheduleRecommendationGenerator generator = new ScheduleRecommendationGenerator();
        using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
        {
            cancellationTokenSource.Cancel();

            ScheduleRecommendationResult result = generator.GenerateRecommendations(
                request,
                cancellationTokenSource.Token);

            Assert.AreEqual(EScheduleRecommendationCompletion.Canceled, result.Completion);
            Assert.IsEmpty(result.Recommendations);
            Assert.IsFalse(result.IsSuccessful);
        }
    }

    [TestMethod]
    public void GenerateRecommendationsKeepsUnscheduledSelectionsSeparateForManualReview()
    {
        CatalogCourse scheduledCourse =
            ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse unscheduledCourse =
            ScheduleRecommendationTestData.CreateCourse("BBB10001");
        CatalogOffering scheduledOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "AAA10001",
                "01",
                new MeetingSlot[]
                {
                    ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Monday, 1),
                });
        CatalogOffering unscheduledOffering =
            ScheduleRecommendationTestData.CreateUnscheduledOffering("BBB10001", "01");
        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { scheduledCourse, unscheduledCourse },
            new CatalogOffering[] { scheduledOffering, unscheduledOffering });
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            new ScheduledCourseChoice[]
            {
                ScheduleRecommendationTestData.CreateChoice("AAA10001", "01"),
            },
            new UnscheduledOfferingSelection[]
            {
                ScheduleRecommendationTestData.CreateUnscheduledSelection("BBB10001", "01"),
            });

        ScheduleRecommendationResult result = generate(catalog, plan, 10);
        ScheduleRecommendation recommendation = result.Recommendations[0];

        Assert.HasCount(1, recommendation.ScheduledOfferings);
        Assert.HasCount(1, recommendation.UnscheduledSelections);
        Assert.AreEqual(
            ERecommendationVerificationStatus.RequiresManualReview,
            recommendation.VerificationStatus);
    }

    [TestMethod]
    public void GenerateRecommendationsRepresentsAnUnscheduledOnlyPlanWithoutFalseConfirmation()
    {
        CatalogCourse course = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogOffering offering = ScheduleRecommendationTestData.CreateUnscheduledOffering(
            "AAA10001",
            "01");
        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { course },
            new CatalogOffering[] { offering });
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            Array.Empty<ScheduledCourseChoice>(),
            new UnscheduledOfferingSelection[]
            {
                ScheduleRecommendationTestData.CreateUnscheduledSelection("AAA10001", "01"),
            });

        ScheduleRecommendationResult result = generate(catalog, plan, 10);
        ScheduleRecommendation recommendation = result.Recommendations[0];

        Assert.HasCount(1, result.Recommendations);
        Assert.IsEmpty(recommendation.ScheduledOfferings);
        Assert.HasCount(1, recommendation.UnscheduledSelections);
        Assert.AreEqual(
            ERecommendationVerificationStatus.RequiresManualReview,
            recommendation.VerificationStatus);
    }

    [TestMethod]
    public void GenerateRecommendationsReturnsNoResultForAnEmptyNewPlan()
    {
        CatalogCourse course = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogOffering offering = ScheduleRecommendationTestData.CreateUnscheduledOffering(
            "AAA10001",
            "01");
        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { course },
            new CatalogOffering[] { offering });
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());

        ScheduleRecommendationResult result = generate(catalog, plan, 10);

        Assert.AreEqual(EScheduleRecommendationCompletion.Completed, result.Completion);
        Assert.IsEmpty(result.Recommendations);
    }

    [TestMethod]
    public void PersonalSchedulePrunesOnlyOverlappingCourseOfferings()
    {
        CatalogCourse course = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogOffering mondayOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "AAA10001",
                "01",
                new MeetingSlot[]
                {
                    ScheduleRecommendationTestData.CreateMeetingSlot(
                        EDay.Monday,
                        1),
                });
        CatalogOffering tuesdayOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "AAA10001",
                "02",
                new MeetingSlot[]
                {
                    ScheduleRecommendationTestData.CreateMeetingSlot(
                        EDay.Tuesday,
                        1),
                });
        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { course },
            new CatalogOffering[] { mondayOffering, tuesdayOffering });
        PersonalSchedule personalSchedule = createPersonalSchedule(
            EDay.Monday,
            new ScheduleTime(9, 15),
            new ScheduleTime(10, 15));
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            new ScheduledCourseChoice[]
            {
                ScheduleRecommendationTestData.CreateChoice(
                    "AAA10001",
                    "01",
                    "02"),
            },
            Array.Empty<UnscheduledOfferingSelection>(),
            new PersonalSchedule[] { personalSchedule });

        ScheduleRecommendationResult result = generate(catalog, plan, 10);

        Assert.HasCount(1, result.Recommendations);
        Assert.AreEqual(
            "02",
            result.Recommendations[0].ScheduledOfferings[0].SectionCode.Value);
        Assert.AreSame(
            personalSchedule,
            result.Recommendations[0].PersonalSchedules[0]);
    }

    [TestMethod]
    public void PersonalOnlyPlanProducesAConfirmedRecommendation()
    {
        CatalogCourse catalogCourse =
            ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogOffering catalogOffering =
            ScheduleRecommendationTestData.CreateUnscheduledOffering(
                "AAA10001",
                "01");
        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { catalogCourse },
            new CatalogOffering[] { catalogOffering });
        PersonalSchedule personalSchedule = createPersonalSchedule(
            EDay.Wednesday,
            new ScheduleTime(12, 20),
            new ScheduleTime(13, 20));
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>(),
            new PersonalSchedule[] { personalSchedule });

        ScheduleRecommendationResult result = generate(catalog, plan, 10);
        ScheduleRecommendation recommendation = result.Recommendations[0];

        Assert.HasCount(1, result.Recommendations);
        Assert.IsEmpty(recommendation.ScheduledOfferings);
        Assert.HasCount(1, recommendation.PersonalSchedules);
        Assert.AreEqual(
            ERecommendationVerificationStatus.ConfirmedConflictFree,
            recommendation.VerificationStatus);
    }

    private static PersonalSchedule createPersonalSchedule(
        EDay day,
        ScheduleTime start,
        ScheduleTime end)
    {
        WeeklyTimeRange timeRange = new WeeklyTimeRange(
            day,
            new DailyTimeRange(start, end));
        return new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("랩 미팅"),
            new WeeklyTimeRange[] { timeRange },
            PersonalScheduleDetails.CreateEmpty());
    }

    private static CourseCatalog createCartesianCatalog()
    {
        CatalogCourse firstCourse = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse secondCourse = ScheduleRecommendationTestData.CreateCourse("BBB10001");
        CatalogOffering firstOption = ScheduleRecommendationTestData.CreateScheduledOffering(
            "AAA10001",
            "01",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Monday, 1),
            });
        CatalogOffering secondOption = ScheduleRecommendationTestData.CreateScheduledOffering(
            "AAA10001",
            "02",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Tuesday, 1),
            });
        CatalogOffering thirdOption = ScheduleRecommendationTestData.CreateScheduledOffering(
            "BBB10001",
            "01",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Wednesday, 1),
            });
        CatalogOffering fourthOption = ScheduleRecommendationTestData.CreateScheduledOffering(
            "BBB10001",
            "02",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Thursday, 1),
            });
        return ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { firstCourse, secondCourse },
            new CatalogOffering[]
            {
                firstOption,
                secondOption,
                thirdOption,
                fourthOption,
            });
    }

    private static PlanningPlan createCartesianPlan(CourseCatalog catalog)
    {
        return ScheduleRecommendationTestData.CreatePlan(
            catalog,
            new ScheduledCourseChoice[]
            {
                ScheduleRecommendationTestData.CreateChoice("AAA10001", "01", "02"),
                ScheduleRecommendationTestData.CreateChoice("BBB10001", "01", "02"),
            },
            Array.Empty<UnscheduledOfferingSelection>());
    }

    private static ScheduleRecommendationResult generate(
        CourseCatalog catalog,
        PlanningPlan plan,
        int recommendationLimitValue)
    {
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(
            catalog,
            plan,
            new ScheduleRecommendationLimit(recommendationLimitValue));
        ScheduleRecommendationGenerator generator = new ScheduleRecommendationGenerator();
        return generator.GenerateRecommendations(request, CancellationToken.None);
    }

    private static string[] getRecommendationNames(ScheduleRecommendationResult result)
    {
        List<string> recommendationNames = new List<string>();
        foreach (ScheduleRecommendation recommendation in result.Recommendations)
        {
            List<string> offeringNames = new List<string>();
            foreach (ScheduledOffering offering in recommendation.ScheduledOfferings)
            {
                offeringNames.Add(
                    getCourseCodeFromId(offering.CourseId)
                    + ":"
                    + offering.SectionCode.Value);
            }

            recommendationNames.Add(string.Join(",", offeringNames));
        }

        return recommendationNames.ToArray();
    }

    private static string getCourseCodeFromId(CourseId courseId)
    {
        int separatorIndex = courseId.Value.LastIndexOf(':');
        return courseId.Value.Substring(separatorIndex + 1);
    }
}
