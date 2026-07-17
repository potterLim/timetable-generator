using System;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Presentation.Catalog;

public sealed class CourseCatalogProjectorTests
{
    [Fact]
    public void ProjectGroupsEveryOfferingAndPreservesScheduleStatusIds()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();

        CourseCatalogProjection projection = CourseCatalogProjector.Project(document);

        Assert.Equal(2, projection.Courses.Count);
        CatalogCourseProjection programmingCourse = projection.FindCourseById(
            new CourseId("course-programming"));
        Assert.Equal(2, programmingCourse.Offerings.Count);
        Assert.Equal(2, programmingCourse.ScheduledOfferingIds.Count);
        Assert.Empty(programmingCourse.TimeNotProvidedOfferingIds);
        Assert.Equal(
            new OfferingId("offering-programming-primary"),
            programmingCourse.ScheduledOfferingIds[0]);
        Assert.Equal(
            new OfferingId("offering-programming-alternative"),
            programmingCourse.ScheduledOfferingIds[1]);

        CatalogCourseProjection seminarCourse = projection.FindCourseById(
            new CourseId("course-seminar"));
        Assert.Empty(seminarCourse.ScheduledOfferingIds);
        Assert.Equal(2, seminarCourse.TimeNotProvidedOfferingIds.Count);
        Assert.Equal(
            new OfferingId("offering-seminar-unscheduled"),
            seminarCourse.TimeNotProvidedOfferingIds[0]);
        Assert.Equal(
            new OfferingId("offering-seminar-unscheduled-02"),
            seminarCourse.TimeNotProvidedOfferingIds[1]);
    }

    [Fact]
    public void ProjectDerivesDynamicOfferingUnitsAndRequirementGroups()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();

        CourseCatalogProjection projection = CourseCatalogProjector.Project(document);

        Assert.Equal(2, projection.OfferingUnitNames.Count);
        Assert.Contains(
            projection.OfferingUnitNames,
            unitName => unitName == new OfferingUnitName("전산전자공학부"));
        Assert.Contains(
            projection.OfferingUnitNames,
            unitName => unitName == new OfferingUnitName("ICT창업학부"));

        Assert.Equal(2, projection.RequirementGroups.Count);
        CatalogRequirementGroup majorRequiredGroup = findRequirementGroup(
            projection,
            ERequirementType.MajorRequired);
        Assert.Single(majorRequiredGroup.Courses);
        Assert.Equal(
            new CourseId("course-programming"),
            majorRequiredGroup.Courses[0].Course.Id);

        CatalogRequirementGroup generalElectiveGroup = findRequirementGroup(
            projection,
            ERequirementType.GeneralElective);
        Assert.Equal(2, generalElectiveGroup.Courses.Count);
    }

    [Fact]
    public void ProjectPreservesTruthfulInstructorLocationAndScheduleSummaries()
    {
        CourseCatalogProjection projection = CourseCatalogProjector.Project(
            CatalogProjectionTestFixture.CreateDocument());

        CatalogOfferingProjection primary = projection.FindOfferingById(
            new OfferingId("offering-programming-primary"));
        Assert.Equal("홍길동 외 1명", primary.InstructorSummary);
        Assert.Equal("오석관 301", primary.LocationSummary);
        Assert.Equal("월 1교시, 수 2교시", primary.ScheduleSummary);

        CatalogOfferingProjection alternative = projection.FindOfferingById(
            new OfferingId("offering-programming-alternative"));
        Assert.Equal("교수 정보 없음", alternative.InstructorSummary);
        Assert.Equal("강의실 미정", alternative.LocationSummary);
        Assert.Equal("화 3교시", alternative.ScheduleSummary);

        CatalogOfferingProjection seminar = projection.FindOfferingById(
            new OfferingId("offering-seminar-unscheduled"));
        Assert.Equal("교수 미정", seminar.InstructorSummary);
        Assert.Equal("강의실 미정", seminar.LocationSummary);
        Assert.Equal("시간 미정 (충돌 자동 검증 제외)", seminar.ScheduleSummary);
    }

    [Fact]
    public void OfferingProjectionRejectsMetadataForAnotherOffering()
    {
        CourseCatalogDocument document = CatalogProjectionTestFixture.CreateDocument();
        CatalogOffering offering = document.Catalog.Offerings[0];
        CatalogOfferingMetadata mismatchedMetadata = document.OfferingMetadata[1];

        Assert.Throws<ArgumentException>(
            () => new CatalogOfferingProjection(offering, mismatchedMetadata));
    }

    [Fact]
    public void ProjectAssignsTheSameAccentRegardlessOfCatalogOrder()
    {
        CourseCatalogProjection originalProjection = CourseCatalogProjector.Project(
            CatalogProjectionTestFixture.CreateDocument());
        CourseCatalogProjection reorderedProjection = CourseCatalogProjector.Project(
            CatalogProjectionTestFixture.CreateReorderedDocument());
        CourseId programmingCourseId = new CourseId("course-programming");
        CourseId seminarCourseId = new CourseId("course-seminar");

        Assert.Equal(
            originalProjection.FindCourseById(programmingCourseId).Accent,
            reorderedProjection.FindCourseById(programmingCourseId).Accent);
        Assert.Equal(
            originalProjection.FindCourseById(seminarCourseId).Accent,
            reorderedProjection.FindCourseById(seminarCourseId).Accent);
    }

    private static CatalogRequirementGroup findRequirementGroup(
        CourseCatalogProjection projection,
        ERequirementType requirementType)
    {
        foreach (CatalogRequirementGroup requirementGroup in projection.RequirementGroups)
        {
            if (requirementGroup.RequirementType == requirementType)
            {
                return requirementGroup;
            }
        }

        throw new InvalidOperationException(
            "The expected requirement group was not projected.");
    }
}
