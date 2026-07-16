using System;

namespace TimetableGenerator.Domain.Scheduling;

public static class AcademicPeriodTimeTable
{
    public static DailyTimeRange GetTimeRange(AcademicPeriod period)
    {
        switch (period.Value)
        {
            case 1:
                return createTimeRange(8, 30, 9, 45);
            case 2:
                return createTimeRange(10, 0, 11, 15);
            case 3:
                return createTimeRange(11, 30, 12, 45);
            case 4:
                return createTimeRange(13, 0, 14, 15);
            case 5:
                return createTimeRange(14, 30, 15, 45);
            case 6:
                return createTimeRange(16, 0, 17, 15);
            case 7:
                return createTimeRange(17, 30, 18, 45);
            case 8:
                return createTimeRange(19, 0, 20, 15);
            case 9:
                return createTimeRange(20, 30, 21, 45);
            case 10:
                return createTimeRange(22, 0, 23, 15);
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
        int startHour,
        int startMinute,
        int endHour,
        int endMinute)
    {
        ScheduleTime start = new ScheduleTime(startHour, startMinute);
        ScheduleTime end = new ScheduleTime(endHour, endMinute);
        return new DailyTimeRange(start, end);
    }
}
