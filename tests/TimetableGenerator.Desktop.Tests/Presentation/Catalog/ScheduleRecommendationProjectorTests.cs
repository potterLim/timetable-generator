using System;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;
using Xunit;
using ApplicationScheduleRecommendation = TimetableGenerator.Application.Scheduling.ScheduleRecommendation;
using PresentationScheduleRecommendation = TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

namespace TimetableGenerator.Desktop.Tests.Presentation.Catalog;

public sealed class ScheduleRecommendationProjectorTests
{
    [Fact]
    public void ProjectCreatesOneScheduleEntryPerSlotUsingCatalogMetadata()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(document);
        ApplicationScheduleRecommendation recommendation = CatalogProjectionTestFixture.CreateRecommendation(document);

        PresentationScheduleRecommendation projectedRecommendation = ScheduleRecommendationProjector.Project(recommendation, catalogProjection);

        Assert.Equal(2, projectedRecommendation.Entries.Count);
        CourseScheduleEntry? mondayEntryOrNull = projectedRecommendation.Entries[0] as CourseScheduleEntry;
        Assert.NotNull(mondayEntryOrNull);
        CourseScheduleEntry mondayEntry = mondayEntryOrNull;
        Assert.Equal("CSE10001", mondayEntry.Code);
        Assert.Equal("프로그래밍 I", mondayEntry.Name);
        Assert.Equal("프로그래밍 I(01)", mondayEntry.NameWithSection);
        Assert.Equal(new CourseSectionCode("01"), mondayEntry.SectionCode);
        Assert.Equal("01분반", mondayEntry.SectionDisplayText);
        ScheduleCardContent cardContent = new ScheduleCardContent(mondayEntry);
        Assert.Equal("프로그래밍 I(01)", cardContent.Title);
        Assert.Equal("오석관 301", cardContent.LocationOrNull);
        Assert.Equal("홍길동 외 1명", cardContent.ResponsiblePersonOrNull);
        Assert.Equal("홍길동 외 1명", mondayEntry.InstructorDisplayText);
        Assert.Equal("오석관 301", mondayEntry.LocationDisplayText);
        Assert.True(mondayEntry.HasConfirmedInstructor);
        Assert.True(mondayEntry.HasAssignedLocation);
        Assert.Equal(EInstructorAssignmentStatus.Confirmed, mondayEntry.CourseDetails.InstructorSummary.AssignmentStatus);
        Assert.Equal(ELocationAssignmentStatus.Assigned, mondayEntry.CourseDetails.LocationSummary.AssignmentStatus);
        Assert.Equal(new CourseCredits(3m), mondayEntry.CourseDetails.Credits);
        Assert.Equal(EDay.Monday, mondayEntry.Day);
        Assert.Equal(new AcademicPeriod(1), mondayEntry.Period);
        Assert.Equal(new MeetingSlot(EDay.Monday, new AcademicPeriod(1)), mondayEntry.Slot);
        Assert.Equal(new DailyTimeRange(new ScheduleTime(9, 0), new ScheduleTime(10, 15)), mondayEntry.TimeRange);
        Assert.Equal(catalogProjection.FindCourseById(new CourseId("course-programming")).Accent, mondayEntry.Accent);

        CourseScheduleEntry? wednesdayEntryOrNull = projectedRecommendation.Entries[1] as CourseScheduleEntry;
        Assert.NotNull(wednesdayEntryOrNull);
        CourseScheduleEntry wednesdayEntry = wednesdayEntryOrNull;
        Assert.Equal(EDay.Wednesday, wednesdayEntry.Day);
        Assert.Equal(new AcademicPeriod(2), wednesdayEntry.Period);
        Assert.Equal(new DailyTimeRange(new ScheduleTime(10, 0), new ScheduleTime(11, 15)), wednesdayEntry.TimeRange);
    }

    [Fact]
    public void ProjectPreservesUnavailableInstructorAndLocationStates()
    {
        CourseCatalogDocument notProvidedDocument = CatalogProjectionTestFixture.CreateDocument();
        CourseCatalogProjection notProvidedCatalog = CourseCatalogProjector.Project(notProvidedDocument);
        ApplicationScheduleRecommendation notProvidedRecommendation = CatalogProjectionTestFixture.CreateScheduledRecommendation(notProvidedDocument, new CourseId("course-programming"), new OfferingId("offering-programming-alternative"));

        PresentationScheduleRecommendation notProvidedProjection = ScheduleRecommendationProjector.Project(notProvidedRecommendation, notProvidedCatalog);

        CourseScheduleEntry notProvidedEntry = Assert.IsType<CourseScheduleEntry>(Assert.Single(notProvidedProjection.Entries));
        Assert.False(notProvidedEntry.HasConfirmedInstructor);
        Assert.False(notProvidedEntry.HasAssignedLocation);
        Assert.Equal("교수 정보 없음", notProvidedEntry.InstructorDisplayText);
        Assert.Equal("강의실 미정", notProvidedEntry.LocationDisplayText);
        Assert.Equal(EInstructorAssignmentStatus.NotProvided, notProvidedEntry.CourseDetails.InstructorSummary.AssignmentStatus);
        Assert.Equal(ELocationAssignmentStatus.NotProvided, notProvidedEntry.CourseDetails.LocationSummary.AssignmentStatus);

        CourseCatalogDocument unconfirmedDocument = CatalogProjectionTestFixture.CreateDocumentWithScheduledAlternativeCourse();
        CourseCatalogProjection unconfirmedCatalog = CourseCatalogProjector.Project(unconfirmedDocument);
        ApplicationScheduleRecommendation unconfirmedRecommendation = CatalogProjectionTestFixture.CreateScheduledRecommendation(unconfirmedDocument, new CourseId("course-seminar"), new OfferingId("offering-seminar-unscheduled"));

        PresentationScheduleRecommendation unconfirmedProjection = ScheduleRecommendationProjector.Project(unconfirmedRecommendation, unconfirmedCatalog);

        CourseScheduleEntry unconfirmedEntry = Assert.IsType<CourseScheduleEntry>(Assert.Single(unconfirmedProjection.Entries));
        Assert.False(unconfirmedEntry.HasConfirmedInstructor);
        Assert.False(unconfirmedEntry.HasAssignedLocation);
        Assert.Equal("교수 미정", unconfirmedEntry.InstructorDisplayText);
        Assert.Equal("강의실 미정", unconfirmedEntry.LocationDisplayText);
        Assert.Equal(EInstructorAssignmentStatus.Unconfirmed, unconfirmedEntry.CourseDetails.InstructorSummary.AssignmentStatus);
        Assert.Equal(ELocationAssignmentStatus.NotProvided, unconfirmedEntry.CourseDetails.LocationSummary.AssignmentStatus);
    }

    [Fact]
    public void ProjectRejectsRecommendationScheduleThatDiffersFromCatalog()
    {
        CourseCatalogDocument sourceDocument = CatalogProjectionTestFixture.CreateDocument();
        ApplicationScheduleRecommendation recommendation = CatalogProjectionTestFixture.CreateRecommendation(sourceDocument);
        CourseCatalogProjection changedCatalogProjection = CourseCatalogProjector.Project(CatalogProjectionTestFixture.CreateDocumentWithChangedPrimarySchedule());

        Assert.Throws<ArgumentException>(
            () => ScheduleRecommendationProjector.Project(
                recommendation,
                changedCatalogProjection));
    }
}
