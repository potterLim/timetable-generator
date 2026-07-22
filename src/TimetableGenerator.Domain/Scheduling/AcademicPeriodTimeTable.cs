using System;

namespace TimetableGenerator.Domain.Scheduling;

public static class AcademicPeriodTimeTable
{
    public static DailyTimeRange GetTimeRange(MeetingSlot slot)
    {
        if (slot.IsValid == false)
        {
            throw new ArgumentException(
                "Academic period time conversion requires a valid meeting slot.",
                nameof(slot));
        }

        if (slot.Day == EDay.Wednesday)
        {
            return getWednesdayTimeRange(slot.Period);
        }

        return getRegularDayTimeRange(slot.Period);
    }

    public static WeeklyTimeRange GetWeeklyTimeRange(MeetingSlot slot)
    {
        return new WeeklyTimeRange(slot.Day, GetTimeRange(slot));
    }

    private static DailyTimeRange getRegularDayTimeRange(AcademicPeriod period)
    {
        switch (period.Value)
        {
            case 1:
                return createTimeRange(new ScheduleTime(9, 0), new ScheduleTime(10, 15));
            case 2:
                return createTimeRange(new ScheduleTime(10, 30), new ScheduleTime(11, 45));
            case 3:
                return createTimeRange(new ScheduleTime(12, 0), new ScheduleTime(13, 15));
            case 4:
                return createTimeRange(new ScheduleTime(13, 30), new ScheduleTime(14, 45));
            case 5:
                return createTimeRange(new ScheduleTime(15, 0), new ScheduleTime(16, 15));
            case 6:
                return createTimeRange(new ScheduleTime(16, 30), new ScheduleTime(17, 45));
            case 7:
                return createTimeRange(new ScheduleTime(18, 0), new ScheduleTime(19, 15));
            case 8:
                return createTimeRange(new ScheduleTime(19, 30), new ScheduleTime(20, 45));
            case 9:
                return createTimeRange(new ScheduleTime(21, 0), new ScheduleTime(22, 15));
            case 10:
                return createTimeRange(new ScheduleTime(22, 30), new ScheduleTime(23, 45));
            default:
                throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown academic period.");
        }
    }

    private static DailyTimeRange getWednesdayTimeRange(AcademicPeriod period)
    {
        switch (period.Value)
        {
            case 1:
                return createTimeRange(new ScheduleTime(8, 30), new ScheduleTime(9, 45));
            case 2:
                return createTimeRange(new ScheduleTime(10, 0), new ScheduleTime(11, 15));
            case 3:
                return createTimeRange(new ScheduleTime(11, 30), new ScheduleTime(12, 45));
            case 4:
            case 5:
            case 6:
            case 7:
            case 8:
            case 9:
            case 10:
                return getRegularDayTimeRange(period);
            default:
                throw new ArgumentOutOfRangeException(nameof(period), period, "Unknown academic period.");
        }
    }

    private static DailyTimeRange createTimeRange(ScheduleTime start, ScheduleTime end)
    {
        return new DailyTimeRange(start, end);
    }
}
