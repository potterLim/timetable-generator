using System;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;
using ApplicationScheduleRecommendation =
    TimetableGenerator.Application.Scheduling.ScheduleRecommendation;
using PresentationScheduleRecommendation =
    TimetableGenerator.Desktop.Presentation.Models.ScheduleRecommendation;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Presentation.Catalog;

public sealed class ScheduleRecommendationProjectorTests
{
    [Fact]
    public void ProjectCreatesOneScheduleEntryPerSlotUsingCatalogMetadata()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(document);
        ApplicationScheduleRecommendation recommendation =
            CatalogProjectionTestFixture.CreateRecommendation(document);

        PresentationScheduleRecommendation projectedRecommendation =
            ScheduleRecommendationProjector.Project(recommendation, catalogProjection);

        Assert.Equal(2, projectedRecommendation.Entries.Count);
        ScheduleEntry mondayEntry = projectedRecommendation.Entries[0];
        Assert.Equal("CSE10001", mondayEntry.Code);
        Assert.Equal("프로그래밍 I", mondayEntry.Name);
        Assert.Equal("홍길동 외 1명 · 3학점", mondayEntry.InstructorDisplayText);
        Assert.Equal("오석관 301", mondayEntry.LocationDisplayText);
        Assert.Equal(EDay.Monday, mondayEntry.Day);
        Assert.Equal(new AcademicPeriod(1), mondayEntry.Period);
        Assert.Equal(
            catalogProjection.FindCourseById(new CourseId("course-programming")).Accent,
            mondayEntry.Accent);

        ScheduleEntry wednesdayEntry = projectedRecommendation.Entries[1];
        Assert.Equal(EDay.Wednesday, wednesdayEntry.Day);
        Assert.Equal(new AcademicPeriod(2), wednesdayEntry.Period);
    }

    [Fact]
    public void ProjectRejectsRecommendationScheduleThatDiffersFromCatalog()
    {
        CourseCatalogDocument sourceDocument = CatalogProjectionTestFixture.CreateDocument();
        ApplicationScheduleRecommendation recommendation =
            CatalogProjectionTestFixture.CreateRecommendation(sourceDocument);
        CourseCatalogProjection changedCatalogProjection = CourseCatalogProjector.Project(
            CatalogProjectionTestFixture.CreateDocumentWithChangedPrimarySchedule());

        Assert.Throws<ArgumentException>(
            () => ScheduleRecommendationProjector.Project(
                recommendation,
                changedCatalogProjection));
    }
}
