using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Domain.Tests.Scheduling;

[TestClass]
public sealed class MeetingScheduleTests
{
    [TestMethod]
    public void NotProvidedScheduleHasAnExplicitEmptyState()
    {
        MeetingSchedule schedule = MeetingSchedule.NotProvided;

        Assert.AreEqual(EMeetingScheduleStatus.NotProvided, schedule.Status);
        Assert.IsFalse(schedule.IsScheduled);
        Assert.IsEmpty(schedule.Slots);
    }

    [TestMethod]
    public void ScheduledMeetingRequiresUniqueValidSlotsAndDefensivelyCopiesThem()
    {
        MeetingSlot mondayFirstPeriod = createSlot(EDay.Monday, 1);
        List<MeetingSlot> mutableSlots = new List<MeetingSlot>()
        {
            mondayFirstPeriod,
        };
        MeetingSchedule schedule = MeetingSchedule.CreateScheduled(mutableSlots);

        mutableSlots.Add(createSlot(EDay.Tuesday, 2));

        Assert.IsTrue(schedule.IsScheduled);
        Assert.HasCount(1, schedule.Slots);
        Assert.AreEqual(mondayFirstPeriod, schedule.Slots[0]);
        Assert.ThrowsExactly<ArgumentException>(
            () => MeetingSchedule.CreateScheduled(Array.Empty<MeetingSlot>()));
        Assert.ThrowsExactly<ArgumentException>(
            () => MeetingSchedule.CreateScheduled(
                new MeetingSlot[] { mondayFirstPeriod, mondayFirstPeriod }));
        Assert.ThrowsExactly<ArgumentException>(
            () => MeetingSchedule.CreateScheduled(new MeetingSlot[] { default(MeetingSlot) }));
    }

    [TestMethod]
    public void MeetingSlotRejectsUnsupportedDaysAndPeriods()
    {
        AcademicPeriod firstPeriod = new AcademicPeriod(1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new MeetingSlot(EDay.None, firstPeriod));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AcademicPeriod(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new AcademicPeriod(11));
        Assert.IsFalse(default(MeetingSlot).IsValid);
    }

    private static MeetingSlot createSlot(EDay day, int periodValue)
    {
        return new MeetingSlot(day, new AcademicPeriod(periodValue));
    }
}
