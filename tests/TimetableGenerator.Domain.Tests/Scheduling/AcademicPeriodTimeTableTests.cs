using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Domain.Tests.Scheduling;

[TestClass]
public sealed class AcademicPeriodTimeTableTests
{
    [TestMethod]
    public void RegularDaysUseTheConfiguredPeriodTimeTable()
    {
        EDay[] regularDays =
        {
            EDay.Monday,
            EDay.Tuesday,
            EDay.Thursday,
            EDay.Friday,
            EDay.Saturday,
            EDay.Sunday,
        };
        ExpectedPeriodTime[] expectedPeriods =
        {
            createExpectedPeriodTime(1, 9, 0, 10, 15),
            createExpectedPeriodTime(2, 10, 30, 11, 45),
            createExpectedPeriodTime(3, 12, 0, 13, 15),
            createExpectedPeriodTime(4, 13, 30, 14, 45),
            createExpectedPeriodTime(5, 15, 0, 16, 15),
            createExpectedPeriodTime(6, 16, 30, 17, 45),
            createExpectedPeriodTime(7, 18, 0, 19, 15),
            createExpectedPeriodTime(8, 19, 30, 20, 45),
            createExpectedPeriodTime(9, 21, 0, 22, 15),
            createExpectedPeriodTime(10, 22, 30, 23, 45),
        };

        foreach (EDay day in regularDays)
        {
            assertTimeTable(day, expectedPeriods);
        }
    }

    [TestMethod]
    public void WednesdayUsesTheConfiguredPeriodTimeTable()
    {
        ExpectedPeriodTime[] expectedPeriods =
        {
            createExpectedPeriodTime(1, 8, 30, 9, 45),
            createExpectedPeriodTime(2, 10, 0, 11, 15),
            createExpectedPeriodTime(3, 11, 30, 12, 45),
            createExpectedPeriodTime(4, 13, 30, 14, 45),
            createExpectedPeriodTime(5, 15, 0, 16, 15),
            createExpectedPeriodTime(6, 16, 30, 17, 45),
            createExpectedPeriodTime(7, 18, 0, 19, 15),
            createExpectedPeriodTime(8, 19, 30, 20, 45),
            createExpectedPeriodTime(9, 21, 0, 22, 15),
            createExpectedPeriodTime(10, 22, 30, 23, 45),
        };

        assertTimeTable(EDay.Wednesday, expectedPeriods);
    }

    [TestMethod]
    public void ConversionRejectsAnInvalidMeetingSlot()
    {
        MeetingSlot invalidSlot = default;

        Assert.ThrowsExactly<ArgumentException>(
            () => AcademicPeriodTimeTable.GetTimeRange(invalidSlot));
        Assert.ThrowsExactly<ArgumentException>(
            () => AcademicPeriodTimeTable.GetWeeklyTimeRange(invalidSlot));
    }

    private static ExpectedPeriodTime createExpectedPeriodTime(
        int periodValue,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute)
    {
        return new ExpectedPeriodTime(
            new AcademicPeriod(periodValue),
            new DailyTimeRange(
                new ScheduleTime(startHour, startMinute),
                new ScheduleTime(endHour, endMinute)));
    }

    private static void assertTimeTable(EDay day, IReadOnlyList<ExpectedPeriodTime> expectedPeriods)
    {
        ScheduleTime? previousEndOrNull = null;
        foreach (ExpectedPeriodTime expectedPeriod in expectedPeriods)
        {
            MeetingSlot slot = new MeetingSlot(day, expectedPeriod.Period);

            DailyTimeRange actualTimeRange = AcademicPeriodTimeTable.GetTimeRange(slot);
            WeeklyTimeRange actualWeeklyTimeRange = AcademicPeriodTimeTable.GetWeeklyTimeRange(slot);

            Assert.AreEqual(expectedPeriod.TimeRange, actualTimeRange);
            Assert.AreEqual(75, actualTimeRange.DurationMinutes);
            Assert.AreEqual(day, actualWeeklyTimeRange.Day);
            Assert.AreEqual(expectedPeriod.TimeRange, actualWeeklyTimeRange.TimeRange);
            if (previousEndOrNull.HasValue)
            {
                Assert.IsLessThan(upperBound: actualTimeRange.Start, value: previousEndOrNull.Value);
            }

            previousEndOrNull = actualTimeRange.End;
        }
    }

    private readonly record struct ExpectedPeriodTime(
        AcademicPeriod Period,
        DailyTimeRange TimeRange);
}
