using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Tests.Scheduling;

[TestClass]
public sealed class ScheduleRecommendationValidationTests
{
    [TestMethod]
    public void GenerateRecommendationsRejectsCatalogBindingMismatch()
    {
        CourseCatalog catalog = createCatalogWithScheduledOfferings();
        PlanCatalogBinding mismatchedBinding = new PlanCatalogBinding(
            new CatalogId("handong-global-university:2026-2:r0002"),
            catalog.InstitutionId,
            catalog.Term,
            new CatalogRevision(2),
            new CatalogArtifactSha256(new string('a', 64)));
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlanWithBinding(
            mismatchedBinding,
            new ScheduledCourseChoice[]
            {
                ScheduleRecommendationTestData.CreateChoice("AAA10001", "01"),
            },
            Array.Empty<UnscheduledOfferingSelection>());

        assertValidationError(
            catalog,
            plan,
            EPlanCatalogValidationError.CatalogBindingMismatch);
    }

    [TestMethod]
    public void GenerateRecommendationsRejectsMissingCourseReference()
    {
        CourseCatalog catalog = createCatalogWithScheduledOfferings();
        ScheduledCourseChoice missingCourseChoice = new ScheduledCourseChoice(
            ScheduleRecommendationTestData.CreateCourseId("CCC10001"),
            new OfferingId[]
            {
                ScheduleRecommendationTestData.CreateOfferingId("AAA10001", "01"),
            });
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            new ScheduledCourseChoice[] { missingCourseChoice },
            Array.Empty<UnscheduledOfferingSelection>());

        assertValidationError(
            catalog,
            plan,
            EPlanCatalogValidationError.CourseNotFound);
    }

    [TestMethod]
    public void GenerateRecommendationsRejectsMissingOfferingReference()
    {
        CourseCatalog catalog = createCatalogWithScheduledOfferings();
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            new ScheduledCourseChoice[]
            {
                ScheduleRecommendationTestData.CreateChoice("AAA10001", "09"),
            },
            Array.Empty<UnscheduledOfferingSelection>());

        assertValidationError(
            catalog,
            plan,
            EPlanCatalogValidationError.OfferingNotFound);
    }

    [TestMethod]
    public void GenerateRecommendationsRejectsOfferingCourseMismatch()
    {
        CourseCatalog catalog = createCatalogWithScheduledOfferings();
        ScheduledCourseChoice mismatchedChoice = new ScheduledCourseChoice(
            ScheduleRecommendationTestData.CreateCourseId("AAA10001"),
            new OfferingId[]
            {
                ScheduleRecommendationTestData.CreateOfferingId("BBB10001", "01"),
            });
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            new ScheduledCourseChoice[] { mismatchedChoice },
            Array.Empty<UnscheduledOfferingSelection>());

        assertValidationError(
            catalog,
            plan,
            EPlanCatalogValidationError.OfferingCourseMismatch);
    }

    [TestMethod]
    public void GenerateRecommendationsRejectsUnscheduledOfferingInScheduledChoice()
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
            new ScheduledCourseChoice[]
            {
                ScheduleRecommendationTestData.CreateChoice("AAA10001", "01"),
            },
            Array.Empty<UnscheduledOfferingSelection>());

        assertValidationError(
            catalog,
            plan,
            EPlanCatalogValidationError.ScheduledChoiceHasNoProvidedTime);
    }

    [TestMethod]
    public void GenerateRecommendationsRejectsScheduledOfferingInUnscheduledSelection()
    {
        CatalogCourse course = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogOffering offering = ScheduleRecommendationTestData.CreateScheduledOffering(
            "AAA10001",
            "01",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Monday, 1),
            });
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

        assertValidationError(
            catalog,
            plan,
            EPlanCatalogValidationError.UnscheduledSelectionHasProvidedTime);
    }

    [TestMethod]
    public void RecommendationRequestRequiresAStrongValidLimit()
    {
        CourseCatalog catalog = createCatalogWithScheduledOfferings();
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());

        Assert.ThrowsExactly<ArgumentException>(
            () => new ScheduleRecommendationRequest(
                catalog,
                plan,
                default(ScheduleRecommendationLimit)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ScheduleRecommendationLimit(0));
    }

    private static CourseCatalog createCatalogWithScheduledOfferings()
    {
        CatalogCourse firstCourse = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse secondCourse = ScheduleRecommendationTestData.CreateCourse("BBB10001");
        CatalogOffering firstOffering = ScheduleRecommendationTestData.CreateScheduledOffering(
            "AAA10001",
            "01",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Monday, 1),
            });
        CatalogOffering secondOffering = ScheduleRecommendationTestData.CreateScheduledOffering(
            "BBB10001",
            "01",
            new MeetingSlot[]
            {
                ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Tuesday, 1),
            });
        return ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { firstCourse, secondCourse },
            new CatalogOffering[] { firstOffering, secondOffering });
    }

    private static void assertValidationError(
        CourseCatalog catalog,
        PlanningPlan plan,
        EPlanCatalogValidationError expectedError)
    {
        ScheduleRecommendationRequest request = new ScheduleRecommendationRequest(
            catalog,
            plan,
            new ScheduleRecommendationLimit(10));
        ScheduleRecommendationGenerator generator = new ScheduleRecommendationGenerator();

        ScheduleRecommendationResult result = generator.GenerateRecommendations(
            request,
            CancellationToken.None);

        Assert.AreEqual(EScheduleRecommendationCompletion.InvalidPlan, result.Completion);
        Assert.AreEqual(expectedError, result.ValidationError);
        Assert.IsTrue(result.HasValidationError);
        Assert.IsEmpty(result.Recommendations);
    }
}
