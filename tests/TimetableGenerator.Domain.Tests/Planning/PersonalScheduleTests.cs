using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Domain.Tests.Planning;

[TestClass]
public sealed class PersonalScheduleTests
{
    [TestMethod]
    public void ScheduleDefensivelyCopiesAndSortsRepeatedDayRanges()
    {
        DailyTimeRange timeRange = createTimeRange(new ScheduleTime(12, 0), new ScheduleTime(13, 0));
        List<WeeklyTimeRange> mutableRanges = new List<WeeklyTimeRange>()
        {
            new WeeklyTimeRange(EDay.Thursday, timeRange),
            new WeeklyTimeRange(EDay.Tuesday, timeRange),
        };

        PersonalSchedule schedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("  랩 미팅  "),
            mutableRanges,
            new PersonalScheduleDetails(
                new PersonalScheduleSection("  A  "),
                new PersonalScheduleInstructor("  김교수  "),
                new PersonalScheduleLocation("  느헤미야홀  ")));

        mutableRanges.Clear();

        Assert.AreEqual("랩 미팅", schedule.Title.Value);
        Assert.HasCount(2, schedule.TimeRanges);
        Assert.AreEqual(EDay.Tuesday, schedule.TimeRanges[0].Day);
        Assert.AreEqual(EDay.Thursday, schedule.TimeRanges[1].Day);
        Assert.AreEqual("A", schedule.Details.SectionOrNull?.Value);
        Assert.AreEqual("김교수", schedule.Details.InstructorOrNull?.Value);
        Assert.AreEqual("느헤미야홀", schedule.Details.LocationOrNull?.Value);
    }

    [TestMethod]
    public void ScheduleRequiresProductTimePrecisionAndMinimumDuration()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => createSchedule(
                PersonalScheduleId.CreateNew(),
                EDay.Monday,
                createTimeRange(
                    new ScheduleTime(12, 1),
                    new ScheduleTime(12, 16))));
        Assert.ThrowsExactly<ArgumentException>(
            () => createSchedule(
                PersonalScheduleId.CreateNew(),
                EDay.Monday,
                createTimeRange(
                    new ScheduleTime(12, 0),
                    new ScheduleTime(12, 10))));

        PersonalSchedule boundarySchedule = createSchedule(
            PersonalScheduleId.CreateNew(),
            EDay.Monday,
            createTimeRange(
                new ScheduleTime(12, 0),
                new ScheduleTime(12, 15)));

        Assert.AreEqual(15, boundarySchedule.TimeRanges[0].TimeRange.DurationMinutes);
    }

    [TestMethod]
    public void ScheduleSupportsTheWholeWeekAndRequiresOneSharedRepeatedTime()
    {
        DailyTimeRange noon = createTimeRange(new ScheduleTime(12, 0), new ScheduleTime(13, 0));
        DailyTimeRange evening = createTimeRange(new ScheduleTime(18, 0), new ScheduleTime(19, 0));

        PersonalSchedule weekendSchedule = new PersonalSchedule(
            PersonalScheduleId.CreateNew(),
            new PersonalScheduleTitle("주말 일정"),
            new WeeklyTimeRange[]
            {
                new WeeklyTimeRange(EDay.Sunday, noon),
                new WeeklyTimeRange(EDay.Saturday, noon),
            },
            PersonalScheduleDetails.CreateEmpty());

        Assert.HasCount(2, weekendSchedule.TimeRanges);
        Assert.AreEqual(EDay.Saturday, weekendSchedule.TimeRanges[0].Day);
        Assert.AreEqual(EDay.Sunday, weekendSchedule.TimeRanges[1].Day);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => createSchedule(PersonalScheduleId.CreateNew(), EDay.None, noon));
        Assert.ThrowsExactly<ArgumentException>(
            () => new PersonalSchedule(
                PersonalScheduleId.CreateNew(),
                new PersonalScheduleTitle("요일별 다른 일정"),
                new WeeklyTimeRange[]
                {
                    new WeeklyTimeRange(EDay.Monday, noon),
                    new WeeklyTimeRange(EDay.Wednesday, evening),
                },
                PersonalScheduleDetails.CreateEmpty()));
    }

    [TestMethod]
    public void PlanContentRejectsDuplicateIdsAndOverlappingSchedules()
    {
        PersonalScheduleId sharedId = PersonalScheduleId.CreateNew();
        PersonalSchedule first = createSchedule(
            sharedId,
            EDay.Tuesday,
            createTimeRange(
                new ScheduleTime(12, 0),
                new ScheduleTime(13, 0)));
        PersonalSchedule duplicateId = createSchedule(
            sharedId,
            EDay.Wednesday,
            createTimeRange(
                new ScheduleTime(12, 0),
                new ScheduleTime(13, 0)));
        PersonalSchedule overlapping = createSchedule(
            PersonalScheduleId.CreateNew(),
            EDay.Tuesday,
            createTimeRange(
                new ScheduleTime(12, 30),
                new ScheduleTime(13, 30)));

        Assert.ThrowsExactly<ArgumentException>(
            () => createPlanContent(new PersonalSchedule[] { first, duplicateId }));
        Assert.ThrowsExactly<ArgumentException>(
            () => createPlanContent(new PersonalSchedule[] { first, overlapping }));
    }

    private static PersonalSchedule createSchedule(
        PersonalScheduleId id,
        EDay day,
        DailyTimeRange timeRange)
    {
        return new PersonalSchedule(
            id,
            new PersonalScheduleTitle("개인 일정"),
            new WeeklyTimeRange[] { new WeeklyTimeRange(day, timeRange) },
            PersonalScheduleDetails.CreateEmpty());
    }

    private static PlanningPlanContent createPlanContent(
        IEnumerable<PersonalSchedule> personalSchedules)
    {
        return new PlanningPlanContent(
            Array.Empty<CourseChoiceGroup>(),
            Array.Empty<UnscheduledOfferingSelection>(),
            personalSchedules);
    }

    private static DailyTimeRange createTimeRange(ScheduleTime start, ScheduleTime end)
    {
        return new DailyTimeRange(start, end);
    }
}
