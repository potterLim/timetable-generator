using System.Linq;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class CourseChoiceDraftCourseItemTests
{
    [Fact]
    public void NewDraftStartsEveryScheduledOfferingAsAcceptable()
    {
        CatalogCourseProjection course = createProgrammingCourseProjection();

        CourseChoiceDraftCourseItem draft =
            CourseChoiceDraftCourseItem.CreateNew(course);

        Assert.Equal(2, draft.Offerings.Count);
        Assert.All(draft.Offerings, offering => Assert.True(offering.IsAcceptable));
        Assert.True(draft.HasEligibleOffering);
    }

    [Fact]
    public void RestoredDraftPreservesSavedPreferenceAndExcludesNewOffering()
    {
        CatalogCourseProjection course = createProgrammingCourseProjection();
        OfferingId savedOfferingId = course.ScheduledOfferingIds[0];
        OfferingCandidate[] savedCandidates =
        {
            new OfferingCandidate(
                savedOfferingId,
                EOfferingPreference.Preferred),
        };

        CourseChoiceDraftCourseItem draft = CourseChoiceDraftCourseItem.Restore(
            course,
            savedCandidates);

        CourseOfferingPreferenceItem savedOffering = draft.Offerings.Single(
            offering => offering.OfferingId == savedOfferingId);
        CourseOfferingPreferenceItem newOffering = draft.Offerings.Single(
            offering => offering.OfferingId != savedOfferingId);
        Assert.True(savedOffering.IsPreferred);
        Assert.True(newOffering.IsExcluded);
    }

    [Fact]
    public void TimeNotProvidedOfferingsParticipateInTheSameDraft()
    {
        CourseCatalogProjection catalog = CourseCatalogProjector.Project(
            CatalogProjectionTestFixture.CreateDocument());
        CatalogCourseProjection course = catalog.Courses.Single(
            candidate => candidate.Course.Code.Value == "BFT30009");

        CourseChoiceDraftCourseItem draft =
            CourseChoiceDraftCourseItem.CreateNew(course);

        Assert.Equal(course.Offerings.Count, draft.Offerings.Count);
        Assert.All(
            draft.Offerings,
            offering =>
            {
                Assert.False(
                    offering.Projection.Offering.MeetingSchedule.IsScheduled);
                Assert.Equal("시간 미정", offering.ScheduleDisplayText);
                Assert.True(offering.IsAcceptable);
            });
    }

    [Fact]
    public void TimeNotProvidedCourseCanBePresentedAsAnAlternative()
    {
        CourseCatalogProjection catalog = CourseCatalogProjector.Project(
            CatalogProjectionTestFixture.CreateDocument());
        CatalogCourseProjection course = catalog.Courses.Single(
            candidate => candidate.Course.Code.Value == "BFT30009");

        CourseChoiceAlternativeSearchItem searchItem =
            new CourseChoiceAlternativeSearchItem(course);

        Assert.Equal(course.Course.Id, searchItem.CourseId);
        Assert.Equal("2개 분반 · 1학점", searchItem.DetailText);
    }

    private static CatalogCourseProjection createProgrammingCourseProjection()
    {
        CourseCatalogProjection catalog = CourseCatalogProjector.Project(
            CatalogProjectionTestFixture.CreateDocument());
        return catalog.Courses.Single(
            course => course.Course.Code.Value == "CSE10001");
    }
}
