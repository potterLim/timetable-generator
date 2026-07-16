using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Domain.Tests.Catalogs;

[TestClass]
public sealed class CourseCatalogTests
{
    [TestMethod]
    public void CatalogPreservesScheduledAndNotProvidedOfferings()
    {
        CatalogCourse course = createCourse("CSE30001");
        CatalogOffering scheduledOffering = createOffering(
            course.Id,
            "01",
            MeetingSchedule.CreateScheduled(
                new MeetingSlot[]
                {
                    new MeetingSlot(EDay.Monday, new AcademicPeriod(1)),
                }));
        CatalogOffering unscheduledOffering = createOffering(
            course.Id,
            "02",
            MeetingSchedule.NotProvided);

        CourseCatalog catalog = createCatalog(
            new CatalogCourse[] { course },
            new CatalogOffering[] { scheduledOffering, unscheduledOffering });

        Assert.HasCount(1, catalog.Courses);
        Assert.HasCount(2, catalog.Offerings);
        Assert.IsTrue(catalog.Offerings[0].MeetingSchedule.IsScheduled);
        Assert.AreEqual(
            EMeetingScheduleStatus.NotProvided,
            catalog.Offerings[1].MeetingSchedule.Status);
    }

    [TestMethod]
    public void CatalogRejectsDuplicateAndOrphanedIdentityReferences()
    {
        CatalogCourse course = createCourse("CSE30001");
        CatalogOffering offering = createOffering(
            course.Id,
            "01",
            MeetingSchedule.NotProvided);
        CatalogOffering orphanedOffering = createOffering(
            new CourseId("handong-global-university:CSE30002"),
            "02",
            MeetingSchedule.NotProvided);

        Assert.ThrowsExactly<ArgumentException>(
            () => createCatalog(
                new CatalogCourse[] { course, course },
                new CatalogOffering[] { offering }));
        Assert.ThrowsExactly<ArgumentException>(
            () => createCatalog(
                new CatalogCourse[] { course },
                new CatalogOffering[] { offering, offering }));
        Assert.ThrowsExactly<ArgumentException>(
            () => createCatalog(
                new CatalogCourse[] { course },
                new CatalogOffering[] { orphanedOffering }));
    }

    [TestMethod]
    public void CatalogDefensivelyCopiesSourceCollections()
    {
        CatalogCourse course = createCourse("CSE30001");
        CatalogOffering offering = createOffering(
            course.Id,
            "01",
            MeetingSchedule.NotProvided);
        List<CatalogCourse> mutableCourses = new List<CatalogCourse>() { course };
        List<CatalogOffering> mutableOfferings = new List<CatalogOffering>() { offering };

        CourseCatalog catalog = createCatalog(mutableCourses, mutableOfferings);

        mutableCourses.Clear();
        mutableOfferings.Clear();

        Assert.HasCount(1, catalog.Courses);
        Assert.HasCount(1, catalog.Offerings);
    }

    private static CatalogCourse createCourse(string courseCodeValue)
    {
        return new CatalogCourse(
            new CourseId("handong-global-university:" + courseCodeValue),
            new CourseCode(courseCodeValue),
            new KoreanCourseName("자료구조"),
            new EnglishCourseName("Data Structures"),
            new CourseCredits(3m));
    }

    private static CatalogOffering createOffering(
        CourseId courseId,
        string sectionCodeValue,
        MeetingSchedule meetingSchedule)
    {
        return new CatalogOffering(
            new OfferingId(
                "handong-global-university:2026-2:"
                + courseId.Value
                + ":"
                + sectionCodeValue),
            courseId,
            new CourseSectionCode(sectionCodeValue),
            meetingSchedule);
    }

    private static CourseCatalog createCatalog(
        IEnumerable<CatalogCourse> courses,
        IEnumerable<CatalogOffering> offerings)
    {
        return new CourseCatalog(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            courses,
            offerings);
    }
}
