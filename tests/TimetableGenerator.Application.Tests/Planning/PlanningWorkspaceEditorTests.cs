using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Application.Tests.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

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
        CourseChoiceGroup choiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01");
        PlanningPlan plan = createPlan(
            "변경 전",
            new CourseChoiceGroup[] { choiceGroup },
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
        Assert.AreSame(choiceGroup, renamedPlan.CourseChoiceGroups[0]);
    }

    [TestMethod]
    public void AddCourseChoiceGroupUpdatesOnlyTheRequestedPlan()
    {
        PlanningPlan firstPlan = createPlan("첫 계획");
        PlanningPlan secondPlan = createPlan("둘째 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            firstPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();
        CourseChoiceGroup addedChoiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01",
                "02");

        PlanningWorkspace result = editor.AddCourseChoiceGroup(
            workspace,
            firstPlan.Id,
            addedChoiceGroup);

        Assert.HasCount(1, result.Plans[0].CourseChoiceGroups);
        Assert.AreSame(addedChoiceGroup, result.Plans[0].CourseChoiceGroups[0]);
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
        Assert.IsEmpty(updatedPlan.CourseChoiceGroups);
        Assert.HasCount(1, updatedPlan.UnscheduledOfferingSelections);
        Assert.AreSame(selection, updatedPlan.UnscheduledOfferingSelections[0]);
    }

    [TestMethod]
    public void RemoveCourseRemovesEitherScheduleStatus()
    {
        CourseChoiceGroup scheduledChoiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01");
        UnscheduledOfferingSelection unscheduledSelection =
            ScheduleRecommendationTestData.CreateUnscheduledSelection(
                "BBB10001",
                "01");
        PlanningPlan plan = createPlan(
            "기본 계획",
            new CourseChoiceGroup[] { scheduledChoiceGroup },
            new UnscheduledOfferingSelection[] { unscheduledSelection });
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();

        PlanningWorkspace withoutScheduled = editor.RemoveCourse(
            workspace,
            plan.Id,
            scheduledChoiceGroup.CourseCandidates[0].CourseId);
        PlanningWorkspace withoutEither = editor.RemoveCourse(
            withoutScheduled,
            plan.Id,
            unscheduledSelection.CourseId);

        Assert.IsEmpty(withoutEither.GetActivePlan().CourseChoiceGroups);
        Assert.IsEmpty(withoutEither.GetActivePlan().UnscheduledOfferingSelections);
    }

    [TestMethod]
    public void PersonalScheduleLifecycleUpdatesOnlyTheRequestedPlan()
    {
        CourseChoiceGroup existingChoiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01");
        PlanningPlan firstPlan = createPlan(
            "첫 계획",
            new CourseChoiceGroup[] { existingChoiceGroup },
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningPlan secondPlan = createPlan("둘째 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            secondPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();
        PersonalSchedule addedSchedule = createPersonalSchedule(
            PersonalScheduleId.CreateNew(),
            "랩 미팅");

        PlanningWorkspace withSchedule = editor.AddPersonalSchedule(
            workspace,
            firstPlan.Id,
            addedSchedule);
        PersonalSchedule updatedSchedule = createPersonalSchedule(
            addedSchedule.Id,
            "연구실 주간 회의");
        PlanningWorkspace withUpdate = editor.UpdatePersonalSchedule(
            withSchedule,
            firstPlan.Id,
            updatedSchedule);
        PlanningWorkspace withoutSchedule = editor.RemovePersonalSchedule(
            withUpdate,
            firstPlan.Id,
            updatedSchedule.Id);

        Assert.HasCount(1, withSchedule.Plans[0].PersonalSchedules);
        Assert.AreSame(addedSchedule, withSchedule.Plans[0].PersonalSchedules[0]);
        Assert.AreSame(
            existingChoiceGroup,
            withSchedule.Plans[0].CourseChoiceGroups[0]);
        Assert.AreSame(secondPlan, withSchedule.Plans[1]);
        Assert.AreEqual(secondPlan.Id, withSchedule.ActivePlanId);
        Assert.AreSame(updatedSchedule, withUpdate.Plans[0].PersonalSchedules[0]);
        Assert.AreSame(
            existingChoiceGroup,
            withUpdate.Plans[0].CourseChoiceGroups[0]);
        Assert.AreSame(secondPlan, withUpdate.Plans[1]);
        Assert.AreEqual(secondPlan.Id, withUpdate.ActivePlanId);
        Assert.IsEmpty(withoutSchedule.Plans[0].PersonalSchedules);
        Assert.AreSame(
            existingChoiceGroup,
            withoutSchedule.Plans[0].CourseChoiceGroups[0]);
        Assert.AreSame(secondPlan, withoutSchedule.Plans[1]);
        Assert.AreEqual(secondPlan.Id, withoutSchedule.ActivePlanId);
    }

    [TestMethod]
    public void CourseAndPlanEditsPreservePersonalSchedules()
    {
        PersonalSchedule existingSchedule = createPersonalSchedule(
            PersonalScheduleId.CreateNew(),
            "고정 일정");
        PlanningPlan plan = createPlan(
            "기본 계획",
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>(),
            new PersonalSchedule[] { existingSchedule });
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();
        CourseChoiceGroup choiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01");

        PlanningWorkspace withCourse = editor.AddCourseChoiceGroup(
            workspace,
            plan.Id,
            choiceGroup);
        PlanningWorkspace renamed = editor.RenamePlan(
            withCourse,
            plan.Id,
            new PlanName("이름 변경"));
        PlanningWorkspace withoutCourse = editor.RemoveCourse(
            renamed,
            plan.Id,
            choiceGroup.CourseCandidates[0].CourseId);

        Assert.AreSame(
            existingSchedule,
            withCourse.GetActivePlan().PersonalSchedules[0]);
        Assert.AreSame(
            existingSchedule,
            renamed.GetActivePlan().PersonalSchedules[0]);
        Assert.AreSame(
            existingSchedule,
            withoutCourse.GetActivePlan().PersonalSchedules[0]);
    }

    [TestMethod]
    public void RecommendationBookmarkIsPlanScopedAndClearedByContentChanges()
    {
        CourseChoiceGroup choiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01",
                "02");
        PlanningPlan firstPlan = createPlan(
            "첫 계획",
            new CourseChoiceGroup[] { choiceGroup },
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningPlan secondPlan = createPlan("둘째 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            secondPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();
        OfferingId selectedOfferingId =
            choiceGroup.CourseCandidates[0].OfferingCandidates[1].OfferingId;
        ScheduleRecommendationBookmark bookmark =
            new ScheduleRecommendationBookmark(
                new OfferingId[] { selectedOfferingId });

        PlanningWorkspace remembered = editor.RememberLastViewedRecommendation(
            workspace,
            firstPlan.Id,
            bookmark);
        PlanningWorkspace renamed = editor.RenamePlan(
            remembered,
            firstPlan.Id,
            new PlanName("이름 변경"));
        PlanningWorkspace withPersonalSchedule = editor.AddPersonalSchedule(
            renamed,
            firstPlan.Id,
            createPersonalSchedule(PersonalScheduleId.CreateNew(), "고정 일정"));

        Assert.AreEqual(secondPlan.Id, remembered.ActivePlanId);
        Assert.AreSame(
            bookmark,
            remembered.Plans[0].LastViewedRecommendationOrNull);
        Assert.IsNull(remembered.Plans[1].LastViewedRecommendationOrNull);
        Assert.AreSame(
            bookmark,
            renamed.Plans[0].LastViewedRecommendationOrNull);
        Assert.IsNull(
            withPersonalSchedule.Plans[0].LastViewedRecommendationOrNull);
    }

    [TestMethod]
    public void ClearPlanContentPreservesPlanIdentityAndClearsEveryContentKind()
    {
        CourseChoiceGroup choiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01",
                "02");
        UnscheduledOfferingSelection unscheduledSelection =
            ScheduleRecommendationTestData.CreateUnscheduledSelection(
                "BBB10001",
                "01");
        PersonalSchedule personalSchedule = createPersonalSchedule(
            PersonalScheduleId.CreateNew(),
            "고정 일정");
        PlanningPlan populatedPlan = createPlan(
            "유지할 계획 이름",
            new CourseChoiceGroup[] { choiceGroup },
            new UnscheduledOfferingSelection[] { unscheduledSelection },
            new PersonalSchedule[] { personalSchedule });
        OfferingId bookmarkedOfferingId =
            choiceGroup.CourseCandidates[0].OfferingCandidates[1].OfferingId;
        PlanningPlan bookmarkedPlan = new PlanningPlan(
            populatedPlan.Id,
            populatedPlan.Name,
            populatedPlan.CatalogBinding,
            populatedPlan.Content,
            new ScheduleRecommendationBookmark(
                new OfferingId[] { bookmarkedOfferingId }));
        PlanningPlan untouchedPlan = createPlan("다른 계획");
        PlanningWorkspace workspace = new PlanningWorkspace(
            bookmarkedPlan.Id,
            new PlanningPlan[] { bookmarkedPlan, untouchedPlan });
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();

        PlanningWorkspace result = editor.ClearPlanContent(
            workspace,
            bookmarkedPlan.Id);

        PlanningPlan clearedPlan = result.GetActivePlan();
        Assert.AreEqual(workspace.ActivePlanId, result.ActivePlanId);
        Assert.HasCount(2, result.Plans);
        Assert.AreEqual(bookmarkedPlan.Id, clearedPlan.Id);
        Assert.AreSame(bookmarkedPlan.Name, clearedPlan.Name);
        Assert.AreSame(bookmarkedPlan.CatalogBinding, clearedPlan.CatalogBinding);
        Assert.IsEmpty(clearedPlan.CourseChoiceGroups);
        Assert.IsEmpty(clearedPlan.UnscheduledOfferingSelections);
        Assert.IsEmpty(clearedPlan.PersonalSchedules);
        Assert.IsNull(clearedPlan.LastViewedRecommendationOrNull);
        Assert.AreSame(untouchedPlan, result.Plans[1]);
        Assert.HasCount(1, bookmarkedPlan.CourseChoiceGroups);
        Assert.HasCount(1, bookmarkedPlan.UnscheduledOfferingSelections);
        Assert.HasCount(1, bookmarkedPlan.PersonalSchedules);
        Assert.IsNotNull(bookmarkedPlan.LastViewedRecommendationOrNull);
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
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
    }

    private static PlanningPlan createPlan(
        string name,
        CourseChoiceGroup[] courseChoiceGroups,
        UnscheduledOfferingSelection[] unscheduledSelections)
    {
        return createPlan(
            name,
            courseChoiceGroups,
            unscheduledSelections,
            Array.Empty<PersonalSchedule>());
    }

    private static PlanningPlan createPlan(
        string name,
        CourseChoiceGroup[] courseChoiceGroups,
        UnscheduledOfferingSelection[] unscheduledSelections,
        PersonalSchedule[] personalSchedules)
    {
        PlanCatalogBinding binding = new PlanCatalogBinding(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('a', 64)));
        return new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName(name),
            binding,
            new PlanningPlanContent(
                courseChoiceGroups,
                unscheduledSelections,
                personalSchedules));
    }

    private static PersonalSchedule createPersonalSchedule(
        PersonalScheduleId id,
        string title)
    {
        WeeklyTimeRange timeRange = new WeeklyTimeRange(
            EDay.Wednesday,
            new DailyTimeRange(
                new ScheduleTime(12, 0),
                new ScheduleTime(13, 0)));
        return new PersonalSchedule(
            id,
            new PersonalScheduleTitle(title),
            new WeeklyTimeRange[] { timeRange },
            PersonalScheduleDetails.CreateEmpty());
    }
}
