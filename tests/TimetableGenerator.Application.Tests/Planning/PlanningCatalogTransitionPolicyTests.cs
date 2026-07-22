using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Application.Tests.Scheduling;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Tests.Planning;

[TestClass]
public sealed class PlanningCatalogTransitionPolicyTests
{
    [TestMethod]
    public void EvaluateTransitionRecognizesExactCatalogBinding()
    {
        CourseCatalog catalog = createCatalog(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1));
        PlanCatalogBinding binding = createBinding(catalog);

        EPlanningCatalogTransitionStatus status =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                binding,
                createBinding(catalog));

        Assert.AreEqual(EPlanningCatalogTransitionStatus.ExactMatch, status);
    }

    [TestMethod]
    public void EvaluateTransitionAllowsOnlyHigherRevisionInSameCatalogFamily()
    {
        CourseCatalog currentCatalog = createCatalog(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1));
        CourseCatalog candidateCatalog = createCatalog(
            new CatalogId("handong-global-university:2026-2:r0002"),
            currentCatalog.InstitutionId,
            currentCatalog.Term,
            new CatalogRevision(2));

        EPlanningCatalogTransitionStatus status =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                createBinding(currentCatalog),
                createBinding(candidateCatalog));

        Assert.AreEqual(EPlanningCatalogTransitionStatus.UpgradeEligible, status);
    }

    [TestMethod]
    public void EvaluateTransitionRejectsDifferentInstitution()
    {
        CourseCatalog currentCatalog = createDefaultCatalog();
        CourseCatalog candidateCatalog = createCatalog(
            new CatalogId("another-university:2026-2:r0002"),
            new InstitutionId("another-university"),
            currentCatalog.Term,
            new CatalogRevision(2));

        EPlanningCatalogTransitionStatus status =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                createBinding(currentCatalog),
                createBinding(candidateCatalog));

        Assert.AreEqual(EPlanningCatalogTransitionStatus.InstitutionMismatch, status);
    }

    [TestMethod]
    public void EvaluateTransitionRejectsDifferentAcademicTerm()
    {
        CourseCatalog currentCatalog = createDefaultCatalog();
        CourseCatalog candidateCatalog = createCatalog(
            new CatalogId("handong-global-university:2027-1:r0002"),
            currentCatalog.InstitutionId,
            AcademicTerm.Parse("2027-1"),
            new CatalogRevision(2));

        EPlanningCatalogTransitionStatus status =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                createBinding(currentCatalog),
                createBinding(candidateCatalog));

        Assert.AreEqual(EPlanningCatalogTransitionStatus.AcademicTermMismatch, status);
    }

    [TestMethod]
    public void EvaluateTransitionRejectsSameOrLowerRevision()
    {
        CourseCatalog currentCatalog = createCatalog(
            new CatalogId("handong-global-university:2026-2:r0002"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(2));
        CourseCatalog sameRevisionCatalog = createCatalog(
            new CatalogId("handong-global-university:2026-2:alternate-r0002"),
            currentCatalog.InstitutionId,
            currentCatalog.Term,
            new CatalogRevision(2));
        CourseCatalog lowerRevisionCatalog = createCatalog(
            new CatalogId("handong-global-university:2026-2:r0001"),
            currentCatalog.InstitutionId,
            currentCatalog.Term,
            new CatalogRevision(1));
        PlanCatalogBinding binding = createBinding(currentCatalog);

        EPlanningCatalogTransitionStatus sameRevisionStatus =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                binding,
                createBinding(sameRevisionCatalog));
        EPlanningCatalogTransitionStatus lowerRevisionStatus =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                binding,
                createBinding(lowerRevisionCatalog));

        Assert.AreEqual(EPlanningCatalogTransitionStatus.RevisionNotNewer, sameRevisionStatus);
        Assert.AreEqual(EPlanningCatalogTransitionStatus.RevisionNotNewer, lowerRevisionStatus);
    }

    [TestMethod]
    public void EvaluateTransitionRejectsChangedArtifactForExactRevision()
    {
        CourseCatalog catalog = createDefaultCatalog();
        PlanCatalogBinding currentBinding = createBinding(catalog);
        PlanCatalogBinding changedArtifactBinding = new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            catalog.Revision,
            new CatalogArtifactSha256(new string('b', 64)));

        EPlanningCatalogTransitionStatus status =
            PlanningCatalogTransitionPolicy.EvaluateTransition(
                currentBinding,
                changedArtifactBinding);

        Assert.AreEqual(EPlanningCatalogTransitionStatus.ArtifactSha256Mismatch, status);
    }

    [TestMethod]
    public void EvaluateTransitionRejectsNullArguments()
    {
        CourseCatalog catalog = createDefaultCatalog();
        PlanCatalogBinding binding = createBinding(catalog);

        Assert.ThrowsExactly<ArgumentNullException>(
            () => PlanningCatalogTransitionPolicy.EvaluateTransition(
                null!,
                binding));
        Assert.ThrowsExactly<ArgumentNullException>(
            () => PlanningCatalogTransitionPolicy.EvaluateTransition(binding, null!));
    }

    private static CourseCatalog createDefaultCatalog()
    {
        return createCatalog(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1));
    }

    private static CourseCatalog createCatalog(
        CatalogId catalogId,
        InstitutionId institutionId,
        AcademicTerm term,
        CatalogRevision revision)
    {
        CatalogCourse course = ScheduleRecommendationTestData.CreateCourse("AAA10001");
        CatalogOffering offering =
            ScheduleRecommendationTestData.CreateScheduledOffering(
                "AAA10001",
                "01",
                new MeetingSlot[]
                {
                    ScheduleRecommendationTestData.CreateMeetingSlot(EDay.Monday, 1),
                });
        return new CourseCatalog(
            catalogId,
            institutionId,
            new InstitutionName("테스트 대학교"),
            term,
            revision,
            new CatalogCourse[] { course },
            new CatalogOffering[] { offering });
    }

    private static PlanCatalogBinding createBinding(CourseCatalog catalog)
    {
        return new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            catalog.Revision,
            new CatalogArtifactSha256(new string('a', 64)));
    }
}
