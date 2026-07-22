using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Domain.Tests.Planning;

[TestClass]
public sealed class ScheduleRecommendationBookmarkTests
{
    [TestMethod]
    public void BookmarkCanonicalizesAndComparesOfferingSets()
    {
        OfferingId firstOfferingId = new OfferingId("offering-01");
        OfferingId secondOfferingId = new OfferingId("offering-02");
        OfferingId[] mutableOfferingIds = new OfferingId[]
        {
            secondOfferingId,
            firstOfferingId,
        };

        ScheduleRecommendationBookmark bookmark = new ScheduleRecommendationBookmark(mutableOfferingIds);
        mutableOfferingIds[0] = new OfferingId("offering-03");

        Assert.AreEqual(firstOfferingId, bookmark.SelectedOfferingIds[0]);
        Assert.AreEqual(secondOfferingId, bookmark.SelectedOfferingIds[1]);
        Assert.IsTrue(bookmark.ContainsOffering(secondOfferingId));
        Assert.IsTrue(bookmark.HasSameOfferingIds(
            new OfferingId[] { secondOfferingId, firstOfferingId }));
        Assert.IsTrue(bookmark.HasSameScheduledOfferingIds(
            new OfferingId[] { secondOfferingId, firstOfferingId }));
        Assert.IsFalse(bookmark.HasSameScheduledOfferingIds(
            new OfferingId[] { firstOfferingId }));
        Assert.ThrowsExactly<ArgumentException>(
            () => new ScheduleRecommendationBookmark(
                Array.Empty<OfferingId>()));
        Assert.ThrowsExactly<ArgumentException>(
            () => new ScheduleRecommendationBookmark(
                new OfferingId[] { firstOfferingId, firstOfferingId }));
        Assert.ThrowsExactly<ArgumentException>(
            () => new ScheduleRecommendationBookmark(
                new OfferingId[] { null! }));
    }

    [TestMethod]
    public void PlanAcceptsOnlyCompleteEligibleRecommendationBookmarks()
    {
        OfferingId preferredOfferingId = new OfferingId("offering-01");
        OfferingId excludedOfferingId = new OfferingId("offering-02");
        CourseCandidate courseCandidate = new CourseCandidate(
            new CourseId("course-01"),
            new OfferingCandidate[]
            {
                new OfferingCandidate(
                    preferredOfferingId,
                    EOfferingPreference.Preferred),
                new OfferingCandidate(
                    excludedOfferingId,
                    EOfferingPreference.Excluded),
            });
        CourseChoiceGroup courseChoiceGroup = new CourseChoiceGroup(
            CourseChoiceGroupId.CreateNew(),
            ECourseChoiceCardinality.ExactlyOne,
            new CourseCandidate[] { courseCandidate });
        PlanningPlanContent content = new PlanningPlanContent(
            new CourseChoiceGroup[] { courseChoiceGroup },
            Array.Empty<UnscheduledOfferingSelection>(),
            Array.Empty<PersonalSchedule>());

        PlanningPlan plan = new PlanningPlan(
            PlanId.CreateNew(),
            new PlanName("추천 복원"),
            createCatalogBinding(),
            content,
            new ScheduleRecommendationBookmark(
                new OfferingId[] { preferredOfferingId }));

        Assert.IsNotNull(plan.LastViewedRecommendationOrNull);
        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningPlan(
                PlanId.CreateNew(),
                new PlanName("제외 분반"),
                createCatalogBinding(),
                content,
                new ScheduleRecommendationBookmark(
                    new OfferingId[] { excludedOfferingId })));
        Assert.ThrowsExactly<ArgumentException>(
            () => new PlanningPlan(
                PlanId.CreateNew(),
                new PlanName("알 수 없는 분반"),
                createCatalogBinding(),
                content,
                new ScheduleRecommendationBookmark(
                    new OfferingId[] { new OfferingId("unknown-offering") })));
    }

    private static PlanCatalogBinding createCatalogBinding()
    {
        return new PlanCatalogBinding(
            new CatalogId("institution:2026-2:r0001"),
            new InstitutionId("institution"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            new CatalogArtifactSha256(new string('a', 64)));
    }
}
