using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Application.Tests.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Tests.Planning;

[TestClass]
public sealed class PlanningWorkspaceCatalogRebinderTests
{
    [TestMethod]
    public void TryRebindPreservesWorkspaceIdentityAndChoices()
    {
        CourseCatalog originalCatalog = createOriginalCatalog();
        CourseChoiceGroup courseChoiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01");
        UnscheduledOfferingSelection unscheduledSelection =
            ScheduleRecommendationTestData.CreateUnscheduledSelection(
                "BBB10001",
                "01");
        PlanningPlan firstPlanWithoutBookmark = createPlan(
            originalCatalog,
            "첫 계획",
            new CourseChoiceGroup[] { courseChoiceGroup },
            Array.Empty<UnscheduledOfferingSelection>());
        OfferingId bookmarkedOfferingId = courseChoiceGroup.CourseCandidates[0]
            .OfferingCandidates[0]
            .OfferingId;
        PlanningPlan firstPlan = new PlanningPlan(
            firstPlanWithoutBookmark.Id,
            firstPlanWithoutBookmark.Name,
            firstPlanWithoutBookmark.CatalogBinding,
            firstPlanWithoutBookmark.Content,
            new ScheduleRecommendationBookmark(
                new OfferingId[] { bookmarkedOfferingId }));
        PlanningPlan secondPlan = createPlan(
            originalCatalog,
            "둘째 계획",
            Array.Empty<CourseChoiceGroup>(),
            new UnscheduledOfferingSelection[] { unscheduledSelection });
        PlanningWorkspace workspace = new PlanningWorkspace(
            secondPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });
        CourseCatalog newCatalog = createCompatibleCatalog();

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(newCatalog, workspace);

        Assert.IsTrue(result.IsRebound);
        Assert.AreEqual(EPlanningWorkspaceCatalogRebindStatus.Rebound, result.Status);
        PlanningWorkspace? reboundWorkspaceOrNull = result.ReboundWorkspaceOrNull;
        Assert.IsNotNull(reboundWorkspaceOrNull);
        Assert.AreNotSame(workspace, reboundWorkspaceOrNull);
        Assert.AreEqual(workspace.ActivePlanId, reboundWorkspaceOrNull.ActivePlanId);
        Assert.HasCount(2, reboundWorkspaceOrNull.Plans);
        assertPlanWasRebound(
            firstPlan,
            reboundWorkspaceOrNull.Plans[0],
            newCatalog);
        assertPlanWasRebound(
            secondPlan,
            reboundWorkspaceOrNull.Plans[1],
            newCatalog);
        Assert.AreSame(
            courseChoiceGroup,
            reboundWorkspaceOrNull.Plans[0].CourseChoiceGroups[0]);
        Assert.AreSame(
            unscheduledSelection,
            reboundWorkspaceOrNull.Plans[1].UnscheduledOfferingSelections[0]);
        Assert.IsNull(
            reboundWorkspaceOrNull.Plans[0].LastViewedRecommendationOrNull);
    }

    [TestMethod]
    public void TryRebindPreservesCrossCourseCandidatesAndPreferences()
    {
        CatalogCourse firstCourse =
            ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse secondCourse =
            ScheduleRecommendationTestData.CreateCourse("BBB10001");
        CatalogOffering preferredOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "AAA10001",
                "01",
                createMondaySlot());
        CatalogOffering excludedOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "AAA10001",
                "02",
                new MeetingSlot[]
                {
                    ScheduleRecommendationTestData.CreateMeetingSlot(
                        EDay.Tuesday,
                        1),
                });
        CatalogOffering acceptableOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "BBB10001",
                "01",
                new MeetingSlot[]
                {
                    ScheduleRecommendationTestData.CreateMeetingSlot(
                        EDay.Wednesday,
                        1),
                });
        CatalogCourse[] courses = new CatalogCourse[] { firstCourse, secondCourse };
        CatalogOffering[] offerings = new CatalogOffering[]
        {
            preferredOffering,
            excludedOffering,
            acceptableOffering,
        };
        CourseCatalog originalCatalog = createCatalog(
            "handong-global-university:2026-2:r0001",
            "2026-2",
            1,
            courses,
            offerings);
        CourseCatalog newCatalog = createCatalog(
            "handong-global-university:2026-2:r0002",
            "2026-2",
            2,
            courses,
            offerings);
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
        CourseChoiceGroup group = new CourseChoiceGroup(
            CourseChoiceGroupId.CreateNew(),
            ECourseChoiceCardinality.ExactlyOne,
            new CourseCandidate[] { firstCandidate, secondCandidate });
        PlanningPlan plan = new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName("선택 계획"),
            createBinding(originalCatalog),
            new PlanningPlanContent(
                new CourseChoiceGroup[] { group },
                Array.Empty<UnscheduledOfferingSelection>(),
                Array.Empty<PersonalSchedule>()));
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });

        PlanningWorkspaceCatalogRebindResult result = tryRebind(
            newCatalog,
            workspace);

        Assert.IsTrue(result.IsRebound);
        Assert.IsNotNull(result.ReboundWorkspaceOrNull);
        CourseChoiceGroup reboundGroup =
            result.ReboundWorkspaceOrNull.Plans[0].CourseChoiceGroups[0];
        Assert.AreSame(group, reboundGroup);
        Assert.AreEqual(
            EOfferingPreference.Excluded,
            reboundGroup.CourseCandidates[0].OfferingCandidates[1].Preference);
    }

    [TestMethod]
    public void TryRebindRejectsAnEmptyPlanForANewAcademicTerm()
    {
        CourseCatalog originalCatalog = createOriginalCatalog();
        PlanningPlan plan = createPlan(
            originalCatalog,
            "빈 계획",
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        CourseCatalog nextTermCatalog = createCatalog(
            "handong-global-university:2027-1:r0001",
            "2027-1",
            1,
            new CatalogCourse[]
            {
                ScheduleRecommendationTestData.CreateCourse("CCC10001"),
            },
            new CatalogOffering[]
            {
                ScheduleRecommendationTestData.CreateScheduledOffering(
                    "CCC10001",
                    "01",
                    new MeetingSlot[]
                    {
                        ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Friday, 1),
                    }),
            });

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(nextTermCatalog, workspace);

        assertFailure(
            result,
            EPlanningWorkspaceCatalogRebindStatus.AcademicTermMismatch);
    }

    [TestMethod]
    public void TryRebindRejectsAnEmptyPlanFromAnotherInstitution()
    {
        CourseCatalog originalCatalog = createOriginalCatalog();
        PlanningPlan plan = createPlan(
            originalCatalog,
            "빈 계획",
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        CourseCatalog otherInstitutionCatalog = createCatalog(
            "another-university:2026-2:r0002",
            new InstitutionId("another-university"),
            "2026-2",
            2,
            originalCatalog.Courses,
            originalCatalog.Offerings);

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(
                otherInstitutionCatalog,
                workspace);

        assertFailure(
            result,
            EPlanningWorkspaceCatalogRebindStatus.InstitutionMismatch);
    }

    [TestMethod]
    public void TryRebindRejectsMixedExistingCatalogBindingsBeforeAnyMigration()
    {
        CourseCatalog originalCatalog = createOriginalCatalog();
        CourseCatalog otherCatalog = createCatalog(
            "handong-global-university:2026-2:r0099",
            "2026-2",
            99,
            originalCatalog.Courses,
            originalCatalog.Offerings);
        PlanningPlan firstPlan = createPlan(
            originalCatalog,
            "첫 계획",
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningPlan secondPlan = createPlan(
            otherCatalog,
            "둘째 계획",
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            firstPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(
                createCompatibleCatalog(),
                workspace);

        assertFailure(
            result,
            EPlanningWorkspaceCatalogRebindStatus.MixedCatalogBindings);
        Assert.AreSame(firstPlan, workspace.Plans[0]);
        Assert.AreSame(secondPlan, workspace.Plans[1]);
    }

    [TestMethod]
    public void TryRebindRejectsChangedArtifactAtCurrentRevision()
    {
        CourseCatalog catalog = createOriginalCatalog();
        PlanningPlan plan = createPlan(
            catalog,
            "기본 계획",
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        PlanCatalogBinding changedArtifactBinding = new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            catalog.Revision,
            new CatalogArtifactSha256(new string('b', 64)));

        PlanningWorkspaceCatalogRebindResult result =
            PlanningWorkspaceCatalogRebinder.TryRebind(
                catalog,
                changedArtifactBinding,
                workspace);

        assertFailure(
            result,
            EPlanningWorkspaceCatalogRebindStatus
                .CatalogArtifactSha256Mismatch);
    }

    [TestMethod]
    public void TryRebindRejectsMissingCourse()
    {
        CourseChoiceGroup choiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01");
        PlanningWorkspace workspace = createWorkspaceWithCourseChoiceGroup(
            choiceGroup);
        CatalogCourse otherCourse =
            ScheduleRecommendationTestData.CreateCourse("CCC10001");
        CatalogOffering otherOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "CCC10001",
                "01",
                createMondaySlot());
        CourseCatalog incompatibleCatalog = createCatalog(
            "handong-global-university:2026-2:r0002",
            "2026-2",
            2,
            new CatalogCourse[] { otherCourse },
            new CatalogOffering[] { otherOffering });

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(
                incompatibleCatalog,
                workspace);

        assertFailure(result, EPlanningWorkspaceCatalogRebindStatus.CourseNotFound);
    }

    [TestMethod]
    public void TryRebindRejectsMissingOffering()
    {
        CourseChoiceGroup choiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01");
        PlanningWorkspace workspace = createWorkspaceWithCourseChoiceGroup(
            choiceGroup);
        OfferingId selectedOfferingId = choiceGroup
            .CourseCandidates[0]
            .OfferingCandidates[0]
            .OfferingId;
        CourseCatalog compatibleCatalog = createCompatibleCatalog();
        List<CatalogOffering> offerings = new List<CatalogOffering>();
        foreach (CatalogOffering offering in compatibleCatalog.Offerings)
        {
            if (offering.Id != selectedOfferingId)
            {
                offerings.Add(offering);
            }
        }

        CourseCatalog incompatibleCatalog = createCatalog(
            compatibleCatalog.Id.Value,
            compatibleCatalog.Term.ToString(),
            compatibleCatalog.Revision.Value,
            compatibleCatalog.Courses,
            offerings);

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(
                incompatibleCatalog,
                workspace);

        assertFailure(result, EPlanningWorkspaceCatalogRebindStatus.OfferingNotFound);
    }

    [TestMethod]
    public void TryRebindRejectsOfferingOwnedByAnotherCourse()
    {
        CourseChoiceGroup choiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01");
        PlanningWorkspace workspace = createWorkspaceWithCourseChoiceGroup(
            choiceGroup);
        OfferingId selectedOfferingId = choiceGroup
            .CourseCandidates[0]
            .OfferingCandidates[0]
            .OfferingId;
        CatalogCourse expectedCourse =
            ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogCourse actualCourse =
            ScheduleRecommendationTestData.CreateCourse("BBB10001");
        CatalogOffering mismatchedOffering = new CatalogOffering(
            selectedOfferingId,
            actualCourse.Id,
            new CourseSectionCode("01"),
            MeetingSchedule.CreateScheduled(createMondaySlot()));
        CourseCatalog incompatibleCatalog = createCatalog(
            "handong-global-university:2026-2:r0002",
            "2026-2",
            2,
            new CatalogCourse[] { expectedCourse, actualCourse },
            new CatalogOffering[] { mismatchedOffering });

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(
                incompatibleCatalog,
                workspace);

        assertFailure(
            result,
            EPlanningWorkspaceCatalogRebindStatus.OfferingCourseMismatch);
    }

    [TestMethod]
    public void TryRebindRejectsScheduledChoiceWhoseTimeBecameNotProvided()
    {
        CourseChoiceGroup choiceGroup =
            ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                "AAA10001",
                "01");
        PlanningWorkspace workspace = createWorkspaceWithCourseChoiceGroup(
            choiceGroup);
        CatalogCourse course = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogOffering unscheduledOffering =
            ScheduleRecommendationTestData.CreateUnscheduledOffering(
                "AAA10001",
                "01");
        CourseCatalog incompatibleCatalog = createCatalog(
            "handong-global-university:2026-2:r0002",
            "2026-2",
            2,
            new CatalogCourse[] { course },
            new CatalogOffering[] { unscheduledOffering });

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(
                incompatibleCatalog,
                workspace);

        assertFailure(
            result,
            EPlanningWorkspaceCatalogRebindStatus.ScheduledChoiceHasNoProvidedTime);
    }

    [TestMethod]
    public void TryRebindRejectsUnscheduledSelectionWhoseTimeBecameProvided()
    {
        CourseCatalog originalCatalog = createOriginalCatalog();
        UnscheduledOfferingSelection selection =
            ScheduleRecommendationTestData.CreateUnscheduledSelection(
                "BBB10001",
                "01");
        PlanningPlan plan = createPlan(
            originalCatalog,
            "기본 계획",
            Array.Empty<CourseChoiceGroup>(),
            new UnscheduledOfferingSelection[] { selection });
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });
        CatalogCourse course = ScheduleRecommendationTestData.CreateCourse("BBB10001");
        CatalogOffering scheduledOffering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "BBB10001",
                "01",
                createMondaySlot());
        CourseCatalog incompatibleCatalog = createCatalog(
            "handong-global-university:2026-2:r0002",
            "2026-2",
            2,
            new CatalogCourse[] { course },
            new CatalogOffering[] { scheduledOffering });

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(
                incompatibleCatalog,
                workspace);

        assertFailure(
            result,
            EPlanningWorkspaceCatalogRebindStatus.UnscheduledSelectionHasProvidedTime);
    }

    [TestMethod]
    public void TryRebindDoesNotExposePartiallyReboundPlansAfterLaterFailure()
    {
        CourseCatalog originalCatalog = createOriginalCatalog();
        PlanningPlan firstPlan = createPlan(
            originalCatalog,
            "첫 계획",
            new CourseChoiceGroup[]
            {
                ScheduleRecommendationTestData.CreateCourseChoiceGroup(
                    "AAA10001",
                    "01"),
            },
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningPlan secondPlan = createPlan(
            originalCatalog,
            "둘째 계획",
            Array.Empty<CourseChoiceGroup>(),
            new UnscheduledOfferingSelection[]
            {
                ScheduleRecommendationTestData.CreateUnscheduledSelection(
                    "BBB10001",
                    "01"),
            });
        PlanningWorkspace workspace = new PlanningWorkspace(
            firstPlan.Id,
            new PlanningPlan[] { firstPlan, secondPlan });
        CourseCatalog incompatibleCatalog = createCatalog(
            "handong-global-university:2026-2:r0002",
            "2026-2",
            2,
            originalCatalog.Courses,
            new CatalogOffering[]
            {
                ScheduleRecommendationTestData.CreateScheduledOffering(
                    "AAA10001",
                    "01",
                    createMondaySlot()),
            });

        PlanningWorkspaceCatalogRebindResult result =
            tryRebind(
                incompatibleCatalog,
                workspace);

        assertFailure(result, EPlanningWorkspaceCatalogRebindStatus.OfferingNotFound);
        Assert.AreSame(firstPlan, workspace.Plans[0]);
        Assert.AreSame(secondPlan, workspace.Plans[1]);
    }

    [TestMethod]
    public void TryRebindRejectsNullArguments()
    {
        CourseCatalog catalog = createOriginalCatalog();
        PlanningPlan plan = createPlan(
            catalog,
            "기본 계획",
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>());
        PlanningWorkspace workspace = new PlanningWorkspace(
            plan.Id,
            new PlanningPlan[] { plan });

        Assert.ThrowsExactly<ArgumentNullException>(
            () => PlanningWorkspaceCatalogRebinder.TryRebind(
                null!,
                createBinding(catalog),
                workspace));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => PlanningWorkspaceCatalogRebinder.TryRebind(
                catalog,
                null!,
                workspace));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => PlanningWorkspaceCatalogRebinder.TryRebind(
                catalog,
                createBinding(catalog),
                null!));
    }

    private static CourseCatalog createOriginalCatalog()
    {
        return createCatalog(
            "handong-global-university:2026-2:r0001",
            "2026-2",
            1,
            new CatalogCourse[]
            {
                ScheduleRecommendationTestData.CreateCourse("AAA10001"),
                ScheduleRecommendationTestData.CreateCourse("BBB10001"),
            },
            new CatalogOffering[]
            {
                ScheduleRecommendationTestData.CreateScheduledOffering(
                    "AAA10001",
                    "01",
                    createMondaySlot()),
                ScheduleRecommendationTestData.CreateUnscheduledOffering(
                    "BBB10001",
                    "01"),
            });
    }

    private static CourseCatalog createCompatibleCatalog()
    {
        CourseCatalog originalCatalog = createOriginalCatalog();
        return createCatalog(
            "handong-global-university:2026-2:r0002",
            "2026-2",
            2,
            originalCatalog.Courses,
            originalCatalog.Offerings);
    }

    private static CourseCatalog createCatalog(
        string catalogIdValue,
        string termValue,
        int revisionValue,
        IEnumerable<CatalogCourse> courses,
        IEnumerable<CatalogOffering> offerings)
    {
        return createCatalog(
            catalogIdValue,
            new InstitutionId("handong-global-university"),
            termValue,
            revisionValue,
            courses,
            offerings);
    }

    private static CourseCatalog createCatalog(
        string catalogIdValue,
        InstitutionId institutionId,
        string termValue,
        int revisionValue,
        IEnumerable<CatalogCourse> courses,
        IEnumerable<CatalogOffering> offerings)
    {
        return new CourseCatalog(
            new CatalogId(catalogIdValue),
            institutionId,
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse(termValue),
            new CatalogRevision(revisionValue),
            courses,
            offerings);
    }

    private static PlanningWorkspace createWorkspaceWithCourseChoiceGroup(
        CourseChoiceGroup choiceGroup)
    {
        CourseCatalog originalCatalog = createOriginalCatalog();
        PlanningPlan plan = createPlan(
            originalCatalog,
            "기본 계획",
            new CourseChoiceGroup[] { choiceGroup },
            Array.Empty<UnscheduledOfferingSelection>());
        return new PlanningWorkspace(plan.Id, new PlanningPlan[] { plan });
    }

    private static PlanningPlan createPlan(
        CourseCatalog catalog,
        string name,
        IEnumerable<CourseChoiceGroup> courseChoiceGroups,
        IEnumerable<UnscheduledOfferingSelection> unscheduledSelections)
    {
        PlanCatalogBinding binding = new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            catalog.Revision,
            createArtifactSha256(catalog));
        return new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName(name),
            binding,
            new PlanningPlanContent(
                courseChoiceGroups,
                unscheduledSelections,
                Array.Empty<PersonalSchedule>()));
    }

    private static MeetingSlot[] createMondaySlot()
    {
        return new MeetingSlot[]
        {
            ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Monday, 1),
        };
    }

    private static void assertPlanWasRebound(
        PlanningPlan originalPlan,
        PlanningPlan reboundPlan,
        CourseCatalog catalog)
    {
        Assert.AreNotSame(originalPlan, reboundPlan);
        Assert.AreEqual(originalPlan.Id, reboundPlan.Id);
        Assert.AreSame(originalPlan.Name, reboundPlan.Name);
        Assert.AreEqual(catalog.Id, reboundPlan.CatalogBinding.CatalogId);
        Assert.AreEqual(
            catalog.InstitutionId,
            reboundPlan.CatalogBinding.InstitutionId);
        Assert.AreEqual(catalog.Term, reboundPlan.CatalogBinding.Term);
        Assert.AreEqual(catalog.Revision, reboundPlan.CatalogBinding.Revision);
        Assert.AreEqual(
            createArtifactSha256(catalog),
            reboundPlan.CatalogBinding.ArtifactSha256);
    }

    private static void assertFailure(
        PlanningWorkspaceCatalogRebindResult result,
        EPlanningWorkspaceCatalogRebindStatus expectedStatus)
    {
        Assert.IsFalse(result.IsRebound);
        Assert.AreEqual(expectedStatus, result.Status);
        Assert.IsNull(result.ReboundWorkspaceOrNull);
    }

    private static PlanningWorkspaceCatalogRebindResult tryRebind(
        CourseCatalog catalog,
        PlanningWorkspace workspace)
    {
        return PlanningWorkspaceCatalogRebinder.TryRebind(
            catalog,
            createBinding(catalog),
            workspace);
    }

    private static PlanCatalogBinding createBinding(CourseCatalog catalog)
    {
        return new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            catalog.Revision,
            createArtifactSha256(catalog));
    }

    private static CatalogArtifactSha256 createArtifactSha256(
        CourseCatalog catalog)
    {
        char hexCharacter = catalog.Revision.Value == 1 ? 'a' : 'b';
        return new CatalogArtifactSha256(new string(hexCharacter, 64));
    }
}
