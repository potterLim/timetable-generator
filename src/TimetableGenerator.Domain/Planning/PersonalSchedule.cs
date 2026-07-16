using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Domain.Planning;

public sealed class PersonalSchedule
{
    private readonly IReadOnlyList<WeeklyTimeRange> mTimeRanges;

    public PersonalScheduleId Id { get; }

    public PersonalScheduleTitle Title { get; }

    public IReadOnlyList<WeeklyTimeRange> TimeRanges
    {
        get
        {
            return mTimeRanges;
        }
    }

    public PersonalScheduleDetails Details { get; }

    public PersonalSchedule(
        PersonalScheduleId id,
        PersonalScheduleTitle title,
        IEnumerable<WeeklyTimeRange> timeRanges,
        PersonalScheduleDetails details)
    {
        if (id.IsValid == false)
        {
            throw new ArgumentException(
                "Personal schedules require a valid ID.",
                nameof(id));
        }

        if (title == null)
        {
            throw new ArgumentNullException(nameof(title));
        }

        if (timeRanges == null)
        {
            throw new ArgumentNullException(nameof(timeRanges));
        }

        if (details == null)
        {
            throw new ArgumentNullException(nameof(details));
        }

        IReadOnlyList<WeeklyTimeRange> copiedTimeRanges =
            copyAndValidateTimeRanges(timeRanges);

        Id = id;
        Title = title;
        mTimeRanges = copiedTimeRanges;
        Details = details;
    }

    private static IReadOnlyList<WeeklyTimeRange> copyAndValidateTimeRanges(
        IEnumerable<WeeklyTimeRange> timeRanges)
    {
        List<WeeklyTimeRange> copiedTimeRanges = new List<WeeklyTimeRange>();
        foreach (WeeklyTimeRange timeRange in timeRanges)
        {
            if (timeRange.IsValid == false)
            {
                throw new ArgumentException(
                    "Personal schedules require valid weekly time ranges.",
                    nameof(timeRanges));
            }

            ensureSupportedWeekday(timeRange.Day, timeRanges);

            foreach (WeeklyTimeRange copiedTimeRange in copiedTimeRanges)
            {
                if (copiedTimeRange.TimeRange != timeRange.TimeRange)
                {
                    throw new ArgumentException(
                        "One personal schedule must use the same time on every day.",
                        nameof(timeRanges));
                }

                if (ScheduleConflictDetector.HasConflict(
                    copiedTimeRange,
                    timeRange))
                {
                    throw new ArgumentException(
                        "A personal schedule cannot contain overlapping time ranges.",
                        nameof(timeRanges));
                }
            }

            copiedTimeRanges.Add(timeRange);
        }

        if (copiedTimeRanges.Count == 0)
        {
            throw new ArgumentException(
                "Personal schedules require at least one weekly time range.",
                nameof(timeRanges));
        }

        copiedTimeRanges.Sort(compareTimeRanges);
        return copiedTimeRanges.AsReadOnly();
    }

    private static void ensureSupportedWeekday(
        EDay day,
        IEnumerable<WeeklyTimeRange> timeRanges)
    {
        switch (day)
        {
            case EDay.Monday:
            case EDay.Tuesday:
            case EDay.Wednesday:
            case EDay.Thursday:
            case EDay.Friday:
                return;
            default:
                throw new ArgumentException(
                    "Personal schedules support weekdays only.",
                    nameof(timeRanges));
        }
    }

    private static int compareTimeRanges(
        WeeklyTimeRange left,
        WeeklyTimeRange right)
    {
        int dayComparison = left.Day.CompareTo(right.Day);
        if (dayComparison != 0)
        {
            return dayComparison;
        }

        return left.TimeRange.Start.CompareTo(right.TimeRange.Start);
    }
}
