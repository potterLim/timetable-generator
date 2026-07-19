using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Application.Tests.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Tests.Planning;

[TestClass]
public sealed class CourseChoiceGroupEditingTests
{
    [TestMethod]
    public void EditorAddsUpdatesAndRemovesCourseChoiceGroups()
    {
        PlanningWorkspace workspace = createEmptyWorkspace();
        PlanningPlan plan = workspace.GetActivePlan();
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();
        CourseChoiceGroup originalGroup = createGroup(
            CourseChoiceGroupId.CreateNew(),
            EOfferingPreference.Acceptable);

        PlanningWorkspace withGroup = editor.AddCourseChoiceGroup(
            workspace,
            plan.Id,
            originalGroup);
        CourseChoiceGroup updatedGroup = createGroup(
            originalGroup.Id,
            EOfferingPreference.Preferred);
        PlanningWorkspace withUpdate = editor.UpdateCourseChoiceGroup(
            withGroup,
            plan.Id,
            updatedGroup);
        PlanningWorkspace withoutGroup = editor.RemoveCourseChoiceGroup(
            withUpdate,
            plan.Id,
            updatedGroup.Id);

        Assert.AreSame(
            originalGroup,
            withGroup.GetActivePlan().CourseChoiceGroups[0]);
        Assert.AreSame(
            updatedGroup,
            withUpdate.GetActivePlan().CourseChoiceGroups[0]);
        Assert.AreEqual(
            EOfferingPreference.Preferred,
            withUpdate.GetActivePlan()
                .CourseChoiceGroups[0]
                .CourseCandidates[0]
                .OfferingCandidates[0]
                .Preference);
        Assert.IsEmpty(withoutGroup.GetActivePlan().CourseChoiceGroups);
    }

    [TestMethod]
    public void RemovingOneCourseRetainsTheOtherCandidateInItsGroup()
    {
        PlanningWorkspace workspace = createEmptyWorkspace();
        PlanningPlan plan = workspace.GetActivePlan();
        PlanningWorkspaceEditor editor = new PlanningWorkspaceEditor();
        CourseChoiceGroup group = createGroup(
            CourseChoiceGroupId.CreateNew(),
            EOfferingPreference.Acceptable);
        PlanningWorkspace withGroup = editor.AddCourseChoiceGroup(
            workspace,
            plan.Id,
            group);

        PlanningWorkspace withoutFirstCourse = editor.RemoveCourse(
            withGroup,
            plan.Id,
            new CourseId("institution:AAA10001"));

        CourseChoiceGroup remainingGroup =
            withoutFirstCourse.GetActivePlan().CourseChoiceGroups[0];
        Assert.AreEqual(group.Id, remainingGroup.Id);
        Assert.HasCount(1, remainingGroup.CourseCandidates);
        Assert.AreEqual(
            new CourseId("institution:BBB10001"),
            remainingGroup.CourseCandidates[0].CourseId);
    }

    [TestMethod]
    public void SessionExposesValidatedGroupEditingForDesktopIntegration()
    {
        CatalogCourse course =
            ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogOffering offering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "AAA10001",
                "01",
                new MeetingSlot[]
                {
                    ScheduleRecommendationTestData.CreateMeetingSlot(
                        EDay.Monday,
                        1),
                });
        CourseCatalog catalog = ScheduleRecommendationTestData.CreateCatalog(
            new CatalogCourse[] { course },
            new CatalogOffering[] { offering });
        PlanningPlan plan = new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName("기본 계획"),
            new PlanCatalogBinding(
                catalog.Id,
                catalog.InstitutionId,
                catalog.Term,
                catalog.Revision,
                new CatalogArtifactSha256(new string('a', 64))),
            new PlanningPlanContent(
                Array.Empty<CourseChoiceGroup>(),
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.CatalogBinding,
            plan.Id,
            new PlanningPlan[] { plan });
        PlanningWorkspaceSession session = new PlanningWorkspaceSession(
            catalog,
            workspace);
        CourseChoiceGroup group = new CourseChoiceGroup(
            CourseChoiceGroupId.CreateNew(),
            ECourseChoiceCardinality.ExactlyOne,
            new CourseCandidate[]
            {
                new CourseCandidate(
                    course.Id,
                    new OfferingCandidate[]
                    {
                        new OfferingCandidate(
                            offering.Id,
                            EOfferingPreference.Acceptable),
                    }),
            });

        session.AddCourseChoiceGroup(group);
        CourseChoiceGroup updatedGroup = new CourseChoiceGroup(
            group.Id,
            group.Cardinality,
            new CourseCandidate[]
            {
                new CourseCandidate(
                    course.Id,
                    new OfferingCandidate[]
                    {
                        new OfferingCandidate(
                            offering.Id,
                            EOfferingPreference.Preferred),
                    }),
            });
        session.UpdateCourseChoiceGroup(updatedGroup);

        Assert.AreSame(
            updatedGroup,
            session.Workspace.GetActivePlan().CourseChoiceGroups[0]);

        session.RemoveCourseChoiceGroup(group.Id);

        Assert.IsEmpty(session.Workspace.GetActivePlan().CourseChoiceGroups);
    }

    private static PlanningWorkspace createEmptyWorkspace()
    {
        PlanCatalogBinding binding = new PlanCatalogBinding(
            new CatalogId("institution:2026-2:r0001"),
            new InstitutionId("institution"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('a', 64)));
        PlanningPlan plan = new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName("기본 계획"),
            binding,
            new PlanningPlanContent(
                Array.Empty<CourseChoiceGroup>(),
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        return new PlanningWorkspace(
            plan.CatalogBinding,
            plan.Id,
            new PlanningPlan[] { plan });
    }

    private static CourseChoiceGroup createGroup(
        CourseChoiceGroupId groupId,
        EOfferingPreference firstPreference)
    {
        CourseCandidate firstCourse = new CourseCandidate(
            new CourseId("institution:AAA10001"),
            new OfferingCandidate[]
            {
                new OfferingCandidate(
                    new OfferingId("institution:term:AAA10001:01"),
                    firstPreference),
            });
        CourseCandidate secondCourse = new CourseCandidate(
            new CourseId("institution:BBB10001"),
            new OfferingCandidate[]
            {
                new OfferingCandidate(
                    new OfferingId("institution:term:BBB10001:01"),
                    EOfferingPreference.Acceptable),
            });
        return new CourseChoiceGroup(
            groupId,
            ECourseChoiceCardinality.ExactlyOne,
            new CourseCandidate[] { firstCourse, secondCourse });
    }
}
