using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleBoardDayRange
{
    private const int TIME_COLUMN_COUNT = 1;
    private const int DEFAULT_VISIBLE_DAY_COUNT = 5;
    private const int SATURDAY_VISIBLE_DAY_COUNT = 6;
    private const int SUNDAY_VISIBLE_DAY_COUNT = 7;

    private static readonly EDay[] ORDERED_DAYS =
    {
        EDay.Monday,
        EDay.Tuesday,
        EDay.Wednesday,
        EDay.Thursday,
        EDay.Friday,
        EDay.Saturday,
        EDay.Sunday,
    };

    private readonly IReadOnlyList<ScheduleBoardDay> mDays;

    public IReadOnlyList<ScheduleBoardDay> Days
    {
        get
        {
            return mDays;
        }
    }

    public int DayCount
    {
        get
        {
            return mDays.Count;
        }
    }

    public int TotalColumnCount
    {
        get
        {
            return TIME_COLUMN_COUNT + mDays.Count;
        }
    }

    private ScheduleBoardDayRange(IReadOnlyList<ScheduleBoardDay> days)
    {
        mDays = days;
    }

    public static ScheduleBoardDayRange CreateForEntries(IReadOnlyList<ScheduleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        int visibleDayCount = findVisibleDayCount(entries);
        List<ScheduleBoardDay> days = new List<ScheduleBoardDay>(visibleDayCount);
        for (int dayIndex = 0; dayIndex < visibleDayCount; ++dayIndex)
        {
            EDay day = ORDERED_DAYS[dayIndex];
            days.Add(new ScheduleBoardDay(
                day,
                TIME_COLUMN_COUNT + dayIndex,
                findShortDisplayName(day),
                FindFullDayDisplayName(day)));
        }

        return new ScheduleBoardDayRange(days.AsReadOnly());
    }

    public ScheduleBoardDay FindDay(EDay day)
    {
        foreach (ScheduleBoardDay boardDay in mDays)
        {
            if (boardDay.Day == day)
            {
                return boardDay;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(day),
            day,
            "The requested day is outside the visible schedule range.");
    }

    public static string FindFullDayDisplayName(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return "월요일";
            case EDay.Tuesday:
                return "화요일";
            case EDay.Wednesday:
                return "수요일";
            case EDay.Thursday:
                return "목요일";
            case EDay.Friday:
                return "금요일";
            case EDay.Saturday:
                return "토요일";
            case EDay.Sunday:
                return "일요일";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Schedule boards require a defined day of the week.");
        }
    }

    public static string CreateFullDayTimeDisplayText(EDay day, DailyTimeRange timeRange)
    {
        ensureValidTimeRange(timeRange);
        return FindFullDayDisplayName(day) + ": " + timeRange;
    }

    public static string CreateShortDayTimeDisplayText(
        IReadOnlyList<EDay> days,
        DailyTimeRange timeRange)
    {
        ArgumentNullException.ThrowIfNull(days);
        if (days.Count == 0)
        {
            throw new ArgumentException("Schedule displays require at least one day.", nameof(days));
        }

        ensureValidTimeRange(timeRange);
        List<string> dayNames = new List<string>(days.Count);
        foreach (EDay day in days)
        {
            dayNames.Add(findShortDisplayName(day));
        }

        return string.Join("·", dayNames) + ": " + timeRange;
    }

    private static int findVisibleDayCount(IReadOnlyList<ScheduleEntry> entries)
    {
        bool hasSaturday = false;
        foreach (ScheduleEntry entry in entries)
        {
            if (entry.Day == EDay.Sunday)
            {
                return SUNDAY_VISIBLE_DAY_COUNT;
            }

            if (entry.Day == EDay.Saturday)
            {
                hasSaturday = true;
            }
        }

        return hasSaturday ? SATURDAY_VISIBLE_DAY_COUNT : DEFAULT_VISIBLE_DAY_COUNT;
    }

    private static string findShortDisplayName(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return "월";
            case EDay.Tuesday:
                return "화";
            case EDay.Wednesday:
                return "수";
            case EDay.Thursday:
                return "목";
            case EDay.Friday:
                return "금";
            case EDay.Saturday:
                return "토";
            case EDay.Sunday:
                return "일";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Schedule boards require a defined day of the week.");
        }
    }

    private static void ensureValidTimeRange(DailyTimeRange timeRange)
    {
        if (timeRange.IsValid == false)
        {
            throw new ArgumentException("Schedule displays require a valid time range.", nameof(timeRange));
        }
    }
}
