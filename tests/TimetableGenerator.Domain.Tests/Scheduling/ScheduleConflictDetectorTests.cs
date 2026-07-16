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

    [TestMethod]
    public void ActualTimeRangesUseHalfOpenOverlapRules()
    {
        WeeklyTimeRange firstRange = createTimeRange(
            EDay.Monday,
            9,
            0,
            10,
            0);
        WeeklyTimeRange touchingRange = createTimeRange(
            EDay.Monday,
            10,
            0,
            11,
            0);
        WeeklyTimeRange overlappingRange = createTimeRange(
            EDay.Monday,
            9,
            59,
            10,
            30);
        WeeklyTimeRange otherDayRange = createTimeRange(
            EDay.Tuesday,
            9,
            30,
            10,
            30);

        Assert.IsFalse(ScheduleConflictDetector.HasConflict(
            firstRange,
            touchingRange));
        Assert.IsTrue(ScheduleConflictDetector.HasConflict(
            firstRange,
            overlappingRange));
        Assert.IsFalse(ScheduleConflictDetector.HasConflict(
            firstRange,
            otherDayRange));
    }

    private static WeeklyTimeRange createTimeRange(
        EDay day,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute)
    {
        DailyTimeRange timeRange = new DailyTimeRange(
            new ScheduleTime(startHour, startMinute),
            new ScheduleTime(endHour, endMinute));
        return new WeeklyTimeRange(day, timeRange);
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
