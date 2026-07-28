using System;
using System.Collections.Generic;
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
    private const int COMBINATORIAL_GROUP_COUNT = 24;

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
        ScheduleRecommendationResult result = session.GenerateRecommendations(new ScheduleRecommendationLimit(10), CancellationToken.None);

        Assert.HasCount(1, session.Workspace.GetActivePlan().CourseChoiceGroups);
        Assert.HasCount(2, result.Recommendations);
        Assert.AreEqual(ERecommendationVerificationStatus.ConfirmedConflictFree, result.Recommendations[0].VerificationStatus);
    }

    [TestMethod]
    public void TimeNotProvidedSelectionRemainsExplicitlyUnverified()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PlanningCourseSelection selection = PlanningCourseSelection.CreateTimeNotProvidedOffering(ScheduleRecommendationTestData.CreateCourseId("BBB10001"), ScheduleRecommendationTestData.CreateOfferingId("BBB10001", "01"));

        session.AddCourse(selection);
        ScheduleRecommendationResult result = session.GenerateRecommendations(new ScheduleRecommendationLimit(10), CancellationToken.None);

        Assert.HasCount(1, session.Workspace.GetActivePlan().UnscheduledOfferingSelections);
        Assert.HasCount(1, result.Recommendations);
        Assert.AreEqual(ERecommendationVerificationStatus.RequiresManualReview, result.Recommendations[0].VerificationStatus);
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
        Assert.IsEmpty(session.Workspace.GetActivePlan().UnscheduledOfferingSelections);
    }

    [TestMethod]
    public void NewPlansUseTheCurrentCatalogBinding()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PlanId newPlanId = PlanId.CreateNew();

        PlanningWorkspace workspace = session.AddPlan(newPlanId, new PlanName("둘째 계획"));

        PlanningPlan plan = workspace.GetActivePlan();
        Assert.AreEqual(newPlanId, plan.Id);
        Assert.AreEqual(catalog.Id, plan.CatalogBinding.CatalogId);
        Assert.AreEqual(catalog.InstitutionId, plan.CatalogBinding.InstitutionId);
        Assert.AreEqual(catalog.Term, plan.CatalogBinding.Term);
        Assert.AreEqual(catalog.Revision, plan.CatalogBinding.Revision);
        Assert.AreEqual(new CatalogArtifactSha256(new string('a', 64)), plan.CatalogBinding.ArtifactSha256);
    }

    [TestMethod]
    public void PersonalSchedulesRemainOwnedByTheirActivePlan()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PersonalSchedule firstPlanSchedule = createPersonalSchedule(PersonalScheduleId.CreateNew(), "첫 계획 일정");
        session.AddPersonalSchedule(firstPlanSchedule);
        session.AddPlan(PlanId.CreateNew(), new PlanName("둘째 계획"));
        PersonalScheduleId secondScheduleId = PersonalScheduleId.CreateNew();
        PersonalSchedule secondPlanSchedule = createPersonalSchedule(secondScheduleId, "둘째 계획 일정");

        session.AddPersonalSchedule(secondPlanSchedule);
        session.UpdatePersonalSchedule(createPersonalSchedule(secondScheduleId, "수정한 둘째 일정"));
        session.RemovePersonalSchedule(secondScheduleId);

        Assert.HasCount(1, session.Workspace.Plans[0].PersonalSchedules);
        Assert.AreSame(firstPlanSchedule, session.Workspace.Plans[0].PersonalSchedules[0]);
        Assert.IsEmpty(session.Workspace.GetActivePlan().PersonalSchedules);
    }

    [TestMethod]
    public void ClearActivePlanContentKeepsTheActivePlanAndOtherPlans()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PlanId firstPlanId = session.Workspace.ActivePlanIdOrNull!.Value;
        PlanName firstPlanName = session.Workspace.GetActivePlan().Name;
        session.AddPersonalSchedule(createPersonalSchedule(PersonalScheduleId.CreateNew(), "첫 계획 일정"));
        PlanId secondPlanId = PlanId.CreateNew();
        session.AddPlan(secondPlanId, new PlanName("둘째 계획"));
        session.AddPersonalSchedule(createPersonalSchedule(PersonalScheduleId.CreateNew(), "둘째 계획 일정"));
        PlanningPlan untouchedFirstPlan = session.Workspace.Plans[0];

        PlanningWorkspace result = session.ClearActivePlanContent();

        PlanningPlan clearedPlan = result.GetActivePlan();
        Assert.AreEqual(secondPlanId, result.ActivePlanIdOrNull);
        Assert.AreEqual(secondPlanId, clearedPlan.Id);
        Assert.AreEqual("둘째 계획", clearedPlan.Name.Value);
        Assert.IsEmpty(clearedPlan.CourseChoiceGroups);
        Assert.IsEmpty(clearedPlan.UnscheduledOfferingSelections);
        Assert.IsEmpty(clearedPlan.PersonalSchedules);
        Assert.IsNull(clearedPlan.LastViewedRecommendationOrNull);
        Assert.AreSame(untouchedFirstPlan, result.Plans[0]);
        Assert.AreEqual(firstPlanId, result.Plans[0].Id);
        Assert.AreSame(firstPlanName, result.Plans[0].Name);
        Assert.HasCount(1, result.Plans[0].PersonalSchedules);
    }

    [TestMethod]
    public void RejectedPersonalScheduleDoesNotMutateTheSession()
    {
        CourseCatalog catalog = createCatalog();
        PlanningWorkspaceSession session = createEmptySession(catalog);
        PersonalSchedule existingSchedule = createPersonalSchedule(PersonalScheduleId.CreateNew(), "기존 일정");
        session.AddPersonalSchedule(existingSchedule);
        PlanningWorkspace workspaceBeforeRejectedEdit = session.Workspace;
        PersonalSchedule overlappingSchedule = createPersonalSchedule(PersonalScheduleId.CreateNew(), "겹치는 일정", new ScheduleTime(12, 30), new ScheduleTime(13, 30));

        Assert.ThrowsExactly<ArgumentException>(
            () => session.AddPersonalSchedule(overlappingSchedule));

        Assert.AreSame(workspaceBeforeRejectedEdit, session.Workspace);
        Assert.HasCount(1, session.Workspace.GetActivePlan().PersonalSchedules);
        Assert.AreSame(existingSchedule, session.Workspace.GetActivePlan().PersonalSchedules[0]);
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
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlanWithBinding(mismatchedBinding, Array.Empty<CourseChoiceGroup>(), Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(plan.CatalogBinding, plan.Id, new PlanningPlan[] { plan });

        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningWorkspaceSession(catalog, workspace));
    }

    [TestMethod]
    public void AddPlanSucceedsWhenTheWorkspaceHasNoPlans()
    {
        CourseCatalog catalog = createCatalog();
        PlanCatalogBinding binding = new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            catalog.Revision,
            new CatalogArtifactSha256(new string('a', 64)));
        PlanningWorkspace workspace = new PlanningWorkspace(binding, null, Array.Empty<PlanningPlan>());
        PlanningWorkspaceSession session = new PlanningWorkspaceSession(catalog, workspace);
        PlanId addedPlanId = PlanId.CreateNew();

        PlanningWorkspace result = session.AddPlan(addedPlanId, new PlanName("새 시간표"));

        Assert.AreSame(binding, result.CatalogBinding);
        Assert.AreEqual(addedPlanId, result.ActivePlanIdOrNull);
        Assert.HasCount(1, result.Plans);
        Assert.AreEqual("새 시간표", result.GetActivePlan().Name.Value);
    }

    [TestMethod]
    public void ActivePlanOperationRejectsWhenTheWorkspaceHasNoPlans()
    {
        CourseCatalog catalog = createCatalog();
        PlanCatalogBinding binding = new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            catalog.Revision,
            new CatalogArtifactSha256(new string('a', 64)));
        PlanningWorkspace workspace = new PlanningWorkspace(binding, null, Array.Empty<PlanningPlan>());
        PlanningWorkspaceSession session = new PlanningWorkspaceSession(catalog, workspace);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => session.ClearActivePlanContent());
        Assert.AreSame(workspace, session.Workspace);
    }

    [TestMethod]
    public void ConstructorValidatesReferencesWithoutSearchingScheduleCombinations()
    {
        List<CatalogCourse> courses = new List<CatalogCourse>();
        List<CatalogOffering> offerings = new List<CatalogOffering>();
        List<CourseChoiceGroup> groups = new List<CourseChoiceGroup>();
        for (int groupIndex = 0; groupIndex < COMBINATORIAL_GROUP_COUNT; ++groupIndex)
        {
            string courseCodeValue = "AAA" + (groupIndex + 1).ToString("D5");
            EDay day = (EDay)((groupIndex / 10) + 1);
            int periodValue = (groupIndex % 10) + 1;
            MeetingSlot slot = ScheduleRecommendationTestData.CreateMeetingSlot(day, periodValue);
            courses.Add(ScheduleRecommendationTestData.CreateCourse(courseCodeValue));
            offerings.Add(ScheduleRecommendationTestData.CreateScheduledOffering(courseCodeValue, "01", new MeetingSlot[] { slot }));
            offerings.Add(ScheduleRecommendationTestData.CreateScheduledOffering(courseCodeValue, "02", new MeetingSlot[] { slot }));
            groups.Add(ScheduleRecommendationTestData.CreateCourseChoiceGroup(courseCodeValue, "01", "02"));
        }

        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(courses, offerings);
        MeetingSlot blockedSlot = ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Wednesday, 4);
        DailyTimeRange blockedTimeRange = AcademicPeriodTimeTable.GetTimeRange(blockedSlot);
        PersonalSchedule blockingSchedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("마지막 후보 차단"),
            new WeeklyTimeRange[]
            {
                new WeeklyTimeRange(blockedSlot.Day, blockedTimeRange),
            },
            PersonalScheduleDetails.CreateEmpty());
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(catalog, groups, Array.Empty<UnscheduledOfferingSelection>(), new PersonalSchedule[] { blockingSchedule });
        PlanningWorkspace workspace = new PlanningWorkspace(plan.CatalogBinding, plan.Id, new PlanningPlan[] { plan });

        PlanningWorkspaceSession session = new PlanningWorkspaceSession(catalog, workspace);

        Assert.AreSame(workspace, session.Workspace);
    }

    private static CourseCatalog createCatalog()
    {
        CatalogCourse scheduledCourse = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse unscheduledCourse = ScheduleRecommendationTestData.CreateCourse("BBB10001");
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
        CatalogOffering unscheduledOffering = ScheduleRecommendationTestData.CreateUnscheduledOffering("BBB10001", "01");
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
        PlanningPlan plan = ScheduleRecommendationTestData.CreatePlan(catalog, Array.Empty<CourseChoiceGroup>(), Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(plan.CatalogBinding, plan.Id, new PlanningPlan[] { plan });
        return new PlanningWorkspaceSession(catalog, workspace);
    }

    private static PersonalSchedule createPersonalSchedule(PersonalScheduleId id, string title)
    {
        return createPersonalSchedule(id, title, new ScheduleTime(12, 0), new ScheduleTime(13, 0));
    }

    private static PersonalSchedule createPersonalSchedule(PersonalScheduleId id, string title, ScheduleTime start, ScheduleTime end)
    {
        WeeklyTimeRange timeRange = new WeeklyTimeRange(EDay.Wednesday, new DailyTimeRange(start, end));
        return new PersonalSchedule(id, new PersonalScheduleTitle(title), new WeeklyTimeRange[] { timeRange }, PersonalScheduleDetails.CreateEmpty());
    }
}
