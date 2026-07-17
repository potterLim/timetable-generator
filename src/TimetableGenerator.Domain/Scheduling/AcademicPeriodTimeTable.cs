using System;

namespace TimetableGenerator.Domain.Scheduling;

public static class AcademicPeriodTimeTable
{
    public static DailyTimeRange GetTimeRange(AcademicPeriod period)
    {
        switch (period.Value)
        {
            case 1:
                return createTimeRange(
                    new ScheduleTime(8, 30),
                    new ScheduleTime(9, 45));
            case 2:
                return createTimeRange(
                    new ScheduleTime(10, 0),
                    new ScheduleTime(11, 15));
            case 3:
                return createTimeRange(
                    new ScheduleTime(11, 30),
                    new ScheduleTime(12, 45));
            case 4:
                return createTimeRange(
                    new ScheduleTime(13, 0),
                    new ScheduleTime(14, 15));
            case 5:
                return createTimeRange(
                    new ScheduleTime(14, 30),
                    new ScheduleTime(15, 45));
            case 6:
                return createTimeRange(
                    new ScheduleTime(16, 0),
                    new ScheduleTime(17, 15));
            case 7:
                return createTimeRange(
                    new ScheduleTime(17, 30),
                    new ScheduleTime(18, 45));
            case 8:
                return createTimeRange(
                    new ScheduleTime(19, 0),
                    new ScheduleTime(20, 15));
            case 9:
                return createTimeRange(
                    new ScheduleTime(20, 30),
                    new ScheduleTime(21, 45));
            case 10:
                return createTimeRange(
                    new ScheduleTime(22, 0),
                    new ScheduleTime(23, 15));
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(period),
                    period,
                    "Unknown academic period.");
        }
    }

    public static WeeklyTimeRange GetWeeklyTimeRange(MeetingSlot slot)
    {
        if (slot.IsValid == false)
        {
            throw new ArgumentException(
                "Academic period time conversion requires a valid meeting slot.",
                nameof(slot));
        }

        return new WeeklyTimeRange(slot.Day, GetTimeRange(slot.Period));
    }

    private static DailyTimeRange createTimeRange(
        ScheduleTime start,
        ScheduleTime end)
    {
        return new DailyTimeRange(start, end);
    }
}
