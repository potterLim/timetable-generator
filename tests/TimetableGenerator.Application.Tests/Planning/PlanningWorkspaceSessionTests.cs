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

        Assert.HasCount(1, session.Workspace.GetActivePlan().CourseChoiceGroups);
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
        Assert.IsEmpty(session.Workspace.GetActivePlan().CourseChoiceGroups);
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
        Assert.AreEqual(catalog.InstitutionId, plan.CatalogBinding.InstitutionId);
        Assert.AreEqual(catalog.Term, plan.CatalogBinding.Term);
        Assert.AreEqual(catalog.Revision, plan.CatalogBinding.Revision);
        Assert.AreEqual(
            new CatalogArtifactSha256(new string('a', 64)),
            plan.CatalogBinding.ArtifactSha256);
    }

    [TestMethod]
    public void PersonalSchedulesRemainOwnedByTheirActivePlan()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PersonalSchedule firstPlanSchedule = createPersonalSchedule(
            PersonalScheduleId.CreateNew(),
            "첫 계획 일정");
        session.AddPersonalSchedule(firstPlanSchedule);
        session.AddPlan(PlanId.CreateNew(), new PlanName("둘째 계획"));
        PersonalScheduleId secondScheduleId = PersonalScheduleId.CreateNew();
        PersonalSchedule secondPlanSchedule = createPersonalSchedule(
            secondScheduleId,
            "둘째 계획 일정");

        session.AddPersonalSchedule(secondPlanSchedule);
        session.UpdatePersonalSchedule(
            createPersonalSchedule(secondScheduleId, "수정한 둘째 일정"));
        session.RemovePersonalSchedule(secondScheduleId);

        Assert.HasCount(1, session.Workspace.Plans[0].PersonalSchedules);
        Assert.AreSame(
            firstPlanSchedule,
            session.Workspace.Plans[0].PersonalSchedules[0]);
        Assert.IsEmpty(session.Workspace.GetActivePlan().PersonalSchedules);
    }

    [TestMethod]
    public void RejectedPersonalScheduleDoesNotMutateTheSession()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PersonalSchedule existingSchedule = createPersonalSchedule(
            PersonalScheduleId.CreateNew(),
            "기존 일정");
        session.AddPersonalSchedule(existingSchedule);
        PlanningWorkspace workspaceBeforeRejectedEdit = session.Workspace;
        PersonalSchedule overlappingSchedule = createPersonalSchedule(
            PersonalScheduleId.CreateNew(),
            "겹치는 일정",
            new ScheduleTime(12, 30),
            new ScheduleTime(13, 30));

        Assert.ThrowsExactly<ArgumentException>(
            () => session.AddPersonalSchedule(overlappingSchedule));

        Assert.AreSame(workspaceBeforeRejectedEdit, session.Workspace);
        Assert.HasCount(1, session.Workspace.GetActivePlan().PersonalSchedules);
        Assert.AreSame(
            existingSchedule,
            session.Workspace.GetActivePlan().PersonalSchedules[0]);
    }

    [TestMethod]
    public void ConstructorRejectsWorkspaceFromAnotherCatalogRevision()
    {
        CourseCatalog catalog = createCatalog();
        PlanCatalogBinding mismatchedBinding = new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            new CatalogRevision(2),
            new CatalogArtifactSha256(new string('a', 64)));
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlanWithBinding(
            mismatchedBinding,
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });

        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspaceSession(catalog, workspace));
    }

    [TestMethod]
    public void ConstructorRejectsMixedArtifactBindingsAtSameRevision()
    {
        CourseCatalog catalog = createCatalog();
        PlanningPlan firstPlan = ScheduleRecommendationTestData.CreatePlan(
            catalog,
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanCatalogBinding changedArtifactBinding = new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            catalog.Revision,
            new CatalogArtifactSha256(new string('b', 64)));
        PlanningPlan secondPlan = new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName("둘째 시간표"),
            changedArtifactBinding,
            new PlanningPlanContent(
                Array.Empty<CourseChoiceGroup>(),
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        PlanningWorkspace workspace = new PlanningWorkspace(
            firstPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });

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
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        return new PlanningWorkspaceSession(catalog, workspace);
    }

    private static PersonalSchedule createPersonalSchedule(
        PersonalScheduleId id,
        string title)
    {
        return createPersonalSchedule(
            id,
            title,
            new ScheduleTime(12, 0),
            new ScheduleTime(13, 0));
    }

    private static PersonalSchedule createPersonalSchedule(
        PersonalScheduleId id,
        string title,
        ScheduleTime start,
        ScheduleTime end)
    {
        WeeklyTimeRange timeRange = new WeeklyTimeRange(
            EDay.Wednesday,
            new DailyTimeRange(start, end));
        return new PersonalSchedule(
            id,
            new PersonalScheduleTitle(title),
            new WeeklyTimeRange[] { timeRange },
            PersonalScheduleDetails.CreateEmpty());
    }
}
