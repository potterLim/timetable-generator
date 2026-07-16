using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Domain.Tests.Scheduling;

[TestClass]
public sealed class ScheduleConflictDetectorTests
{
    [TestMethod]
    public void ConflictDetectionIsSymmetricAndDeterministic()
    {
        ScheduledOffering firstOffering = createScheduledOffering(
            "CSE30001",
            "01",
            EDay.Monday,
            1);
        ScheduledOffering secondOffering = createScheduledOffering(
            "CSE30002",
            "01",
            EDay.Monday,
            1);

        bool firstResult = ScheduleConflictDetector.HasConflict(
            firstOffering,
            secondOffering);
        bool reversedResult = ScheduleConflictDetector.HasConflict(
            secondOffering,
            firstOffering);
        bool repeatedResult = ScheduleConflictDetector.HasConflict(
            firstOffering,
            secondOffering);

        Assert.IsTrue(firstResult);
        Assert.AreEqual(firstResult, reversedResult);
        Assert.AreEqual(firstResult, repeatedResult);
    }

    [TestMethod]
    public void DifferentMeetingSlotsDoNotConflict()
    {
        ScheduledOffering firstOffering = createScheduledOffering(
            "CSE30001",
            "01",
            EDay.Monday,
            1);
        ScheduledOffering secondOffering = createScheduledOffering(
            "CSE30002",
            "01",
            EDay.Tuesday,
            1);

        Assert.IsFalse(ScheduleConflictDetector.HasConflict(firstOffering, secondOffering));
    }

    [TestMethod]
    public void UnscheduledCatalogOfferingCannotBecomeAScheduledProjection()
    {
        CatalogOffering unscheduledOffering = new CatalogOffering(
            new OfferingId("handong-global-university:2026-2:CSE30001:01"),
            new CourseId("handong-global-university:CSE30001"),
            new CourseSectionCode("01"),
            MeetingSchedule.NotProvided);

        Assert.ThrowsExactly<ArgumentException>(
            () => new ScheduledOffering(unscheduledOffering));
    }

    private static ScheduledOffering createScheduledOffering(
        string courseCodeValue,
        string sectionCodeValue,
        EDay day,
        int periodValue)
    {
        CourseId courseId = new CourseId("handong-global-university:" + courseCodeValue);
        CatalogOffering catalogOffering = new CatalogOffering(
            new OfferingId(
                "handong-global-university:2026-2:"
                + courseCodeValue
                + ":"
                + sectionCodeValue),
            courseId,
            new CourseSectionCode(sectionCodeValue),
            MeetingSchedule.CreateScheduled(
                new MeetingSlot[]
                {
                    new MeetingSlot(day, new AcademicPeriod(periodValue)),
                }));
        return new ScheduledOffering(catalogOffering);
    }
}
