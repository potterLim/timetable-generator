using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Application.Scheduling;
using TimetableGenerator.Application.Tests.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Tests.Planning;

[TestClass]
public sealed class PlanningWorkspaceSessionTests
{
    [TestMethod]
    public void ScheduledSelectionUsesEverySectionAsARecommendationAlternative()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PlanningCourseSelection selection =
            PlanningCourseSelection.CreateScheduledAlternatives(
                ScheduleRecommendationTestData.CreateCourseId("AAA10001"),
                new OfferingId[]
                {
                    ScheduleRecommendationTestData.CreateOfferingId("AAA10001", "01"),
                    ScheduleRecommendationTestData.CreateOfferingId("AAA10001", "02"),
                });

        session.AddCourse(selection);
        ScheduleRecommendationResult result = session.GenerateRecommendations(
            new ScheduleRecommendationLimit(10),
            CancellationToken.None);

        Assert.HasCount(1, session.Workspace.GetActivePlan().ScheduledCourseChoices);
        Assert.HasCount(2, result.Recommendations);
        Assert.AreEqual(
            ERecommendationVerificationStatus.ConfirmedConflictFree,
            result.Recommendations[0].VerificationStatus);
    }

    [TestMethod]
    public void TimeNotProvidedSelectionRemainsExplicitlyUnverified()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PlanningCourseSelection selection =
            PlanningCourseSelection.CreateTimeNotProvidedOffering(
                ScheduleRecommendationTestData.CreateCourseId("BBB10001"),
                ScheduleRecommendationTestData.CreateOfferingId("BBB10001", "01"));

        session.AddCourse(selection);
        ScheduleRecommendationResult result = session.GenerateRecommendations(
            new ScheduleRecommendationLimit(10),
            CancellationToken.None);

        Assert.HasCount(
            1,
            session.Workspace.GetActivePlan().UnscheduledOfferingSelections);
        Assert.HasCount(1, result.Recommendations);
        Assert.AreEqual(
            ERecommendationVerificationStatus.RequiresManualReview,
            result.Recommendations[0].VerificationStatus);
    }

    [TestMethod]
    public void InvalidScheduleStatusDoesNotMutateTheSession()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PlanningCourseSelection invalidSelection =
            PlanningCourseSelection.CreateScheduledAlternatives(
                ScheduleRecommendationTestData.CreateCourseId("BBB10001"),
                new OfferingId[]
                {
                    ScheduleRecommendationTestData.CreateOfferingId("BBB10001", "01"),
                });

        Assert.ThrowsExactly<ArgumentException>(
            () => session.AddCourse(invalidSelection));
        Assert.IsEmpty(session.Workspace.GetActivePlan().ScheduledCourseChoices);
        Assert.IsEmpty(
            session.Workspace.GetActivePlan().UnscheduledOfferingSelections);
    }

    [TestMethod]
    public void NewPlansUseTheCurrentCatalogBinding()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PlanId newPlanId = PlanId.CreateNew();

        PlanningWorkspace workspace = session.AddPlan(
            newPlanId,
            new PlanName("둘째 계획"));

        PlanningPlan plan = workspace.GetActivePlan();
        Assert.AreEqual(newPlanId, plan.Id);
        Assert.AreEqual(catalog.Id, plan.CatalogBinding.CatalogId);
        Assert.AreEqual(catalog.Term, plan.CatalogBinding.Term);
        Assert.AreEqual(catalog.Revision, plan.CatalogBinding.Revision);
    }

    [TestMethod]
    public void ConstructorRejectsWorkspaceFromAnotherCatalogRevision()
    {
        CourseCatalog catalog = createCatalog();
        PlanCatalogBinding mismatchedBinding = new PlanCatalogBinding(
            catalog.Id,
            catalog.Term,
            new CatalogRevision(2));
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlanWithBinding(
            mismatchedBinding,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });

        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspaceSession(catalog, workspace));
    }

    private static CourseCatalog createCatalog()
    {
        CatalogCourse scheduledCourse =
            ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse unscheduledCourse =
            ScheduleRecommendationTestData.CreateCourse("BBB10001");
        CatalogOffering firstScheduledOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "AAA10001",
                "01",
                new MeetingSlot[]
                {
                    ScheduleRecommendationTestData.CreateMeetingSlot(
                        EDay.Monday,
                        1),
                });
        CatalogOffering secondScheduledOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "AAA10001",
                "02",
                new MeetingSlot[]
                {
                    ScheduleRecommendationTestData.CreateMeetingSlot(
                        EDay.Tuesday,
                        1),
                });
        CatalogOffering unscheduledOffering =
            ScheduleRecommendationTestData.CreateUnscheduledOffering(
                "BBB10001",
                "01");
        return ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { scheduledCourse, unscheduledCourse },
            new CatalogOffering[]
            {
                firstScheduledOffering,
                secondScheduledOffering,
                unscheduledOffering,
            });
    }

    private static PlanningWorkspaceSession createEmptySession(CourseCatalog catalog)
    {
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            Array.Empty<ScheduledCourseChoice>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        return new PlanningWorkspaceSession(catalog, workspace);
    }
}
