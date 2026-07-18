using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleBoardLayoutTests
{
    [Fact]
    public void DefaultLayoutShowsWeekdaysFromTenAmThroughSevenPm()
    {
        ScheduleBoardLayout layout = ScheduleBoardLayout.Default;

        Assert.Equal(5, layout.DayRange.DayCount);
        Assert.Equal(EDay.Monday, layout.DayRange.Days[0].Day);
        Assert.Equal(EDay.Friday, layout.DayRange.Days[4].Day);
        Assert.Equal(new ScheduleBoardTimeBoundary(600), layout.TimeAxis.Start);
        Assert.Equal(new ScheduleBoardTimeBoundary(1_140), layout.TimeAxis.End);
        Assert.Equal(108, layout.TimeAxis.IncrementCount);
        Assert.Equal(18, layout.TimeAxis.LabelTimes.Count);
        Assert.Equal("10:00", layout.TimeAxis.LabelTimes[0].ToString());
        Assert.Equal("18:30", layout.TimeAxis.LabelTimes[^1].ToString());
    }

    [Fact]
    public void SaturdayEntryExtendsVisibleDaysThroughSaturday()
    {
        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForEntries(
            new ScheduleEntry[]
            {
                createEntry(
                    EDay.Saturday,
                    new ScheduleTime(12, 0),
                    new ScheduleTime(13, 0)),
            });

        Assert.Equal(6, layout.DayRange.DayCount);
        Assert.Equal(EDay.Saturday, layout.DayRange.Days[5].Day);
        Assert.Equal(6, layout.DayRange.FindDay(EDay.Saturday).ColumnIndex);
    }

    [Fact]
    public void SundayEntryExtendsVisibleDaysThroughBothWeekendDays()
    {
        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForEntries(
            new ScheduleEntry[]
            {
                createEntry(
                    EDay.Sunday,
                    new ScheduleTime(12, 0),
                    new ScheduleTime(13, 0)),
            });

        Assert.Equal(7, layout.DayRange.DayCount);
        Assert.Equal(EDay.Saturday, layout.DayRange.Days[5].Day);
        Assert.Equal(EDay.Sunday, layout.DayRange.Days[6].Day);
        Assert.Equal(7, layout.DayRange.FindDay(EDay.Sunday).ColumnIndex);
    }

    [Fact]
    public void DayAndTimeDisplaysUseAColonBetweenTheLabelAndValue()
    {
        DailyTimeRange timeRange = new DailyTimeRange(
            new ScheduleTime(11, 30),
            new ScheduleTime(12, 15));

        Assert.Equal(
            "월요일: 11:30–12:15",
            ScheduleBoardDayRange.CreateFullDayTimeDisplayText(
                EDay.Monday,
                timeRange));
        Assert.Equal(
            "월·수: 11:30–12:15",
            ScheduleBoardDayRange.CreateShortDayTimeDisplayText(
                new[] { EDay.Monday, EDay.Wednesday },
                timeRange));
    }

    [Fact]
    public void TimeAxisExpandsContinuouslyToHalfHourBoundaries()
    {
        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        entries.Add(createEntry(
            EDay.Monday,
            new ScheduleTime(7, 40),
            new ScheduleTime(8, 20)));
        entries.Add(createEntry(
            EDay.Friday,
            new ScheduleTime(20, 10),
            new ScheduleTime(20, 20)));

        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForEntries(entries);

        Assert.Equal(new ScheduleBoardTimeBoundary(450), layout.TimeAxis.Start);
        Assert.Equal(new ScheduleBoardTimeBoundary(1_230), layout.TimeAxis.End);
        Assert.Equal(156, layout.TimeAxis.IncrementCount);
        Assert.Equal("07:30", layout.TimeAxis.LabelTimes[0].ToString());
        Assert.Equal(
            "20:00",
            layout.TimeAxis.LabelTimes[
                layout.TimeAxis.LabelTimes.Count - 1].ToString());
    }

    [Fact]
    public void TimeAxisCanRoundTheFinalEntryThroughEndOfDay()
    {
        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForEntries(
            new ScheduleEntry[]
            {
                createEntry(
                    EDay.Friday,
                    new ScheduleTime(23, 30),
                    new ScheduleTime(23, 59)),
            });

        Assert.Equal(new ScheduleBoardTimeBoundary(1_440), layout.TimeAxis.End);
        Assert.Equal("23:30", layout.TimeAxis.LabelTimes[
            layout.TimeAxis.LabelTimes.Count - 1].ToString());
    }

    [Theory]
    [InlineData(10, 5, 10, 35, 600)]
    [InlineData(11, 0, 11, 30, 600)]
    [InlineData(11, 30, 12, 0, 660)]
    [InlineData(14, 0, 14, 30, 780)]
    [InlineData(14, 30, 15, 0, 840)]
    [InlineData(20, 0, 20, 30, 1_140)]
    public void LateTimeAxisStartsAtAContextualWholeHour(
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        int expectedStartMinute)
    {
        ScheduleBoardLayout layout = ScheduleBoardLayout.CreateForEntries(
            new ScheduleEntry[]
            {
                createEntry(
                    EDay.Monday,
                    new ScheduleTime(startHour, startMinute),
                    new ScheduleTime(endHour, endMinute)),
            });

        Assert.Equal(
            new ScheduleBoardTimeBoundary(expectedStartMinute),
            layout.TimeAxis.Start);
    }

    [Fact]
    public void ScheduleEntryRejectsUndefinedDay()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            delegate
            {
                createEntry(
                    EDay.None,
                    new ScheduleTime(9, 0),
                    new ScheduleTime(10, 0));
            });
    }

    private static ScheduleEntry createEntry(
        EDay day,
        ScheduleTime start,
        ScheduleTime end)
    {
        return new TestScheduleEntry(day, new DailyTimeRange(start, end));
    }

    private sealed class TestScheduleEntry : ScheduleEntry
    {
        public TestScheduleEntry(EDay day, DailyTimeRange timeRange)
            : base(day, timeRange)
        {
        }
    }
}
