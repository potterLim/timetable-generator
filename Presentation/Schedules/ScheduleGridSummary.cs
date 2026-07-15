using System;
using System.Collections.Generic;
using CoreDay = TimetableGenerator.Core.Domain.EDay;

namespace TimetableGenerator.Presentation.Schedules;

public sealed class ScheduleGridSummary
{
    public int SelectedCourseCount { get; }

    public int ScheduledMeetingCount { get; }

    private readonly IReadOnlyList<CoreDay> mActiveDays;

    public IReadOnlyList<CoreDay> ActiveDays
    {
        get
        {
            return mActiveDays;
        }
    }

    public int ActiveDayCount
    {
        get
        {
            return mActiveDays.Count;
        }
    }

    public bool HasWeekendClasses
    {
        get
        {
            return hasActiveDay(CoreDay.Saturday) || hasActiveDay(CoreDay.Sunday);
        }
    }

    internal ScheduleGridSummary(
        int selectedCourseCount,
        int scheduledMeetingCount,
        IEnumerable<CoreDay> activeDays)
    {
        if (selectedCourseCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedCourseCount));
        }

        if (scheduledMeetingCount < selectedCourseCount)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduledMeetingCount));
        }

        if (activeDays == null)
        {
            throw new ArgumentNullException(nameof(activeDays));
        }

        List<CoreDay> copiedActiveDays = new List<CoreDay>();
        HashSet<CoreDay> uniqueActiveDays = new HashSet<CoreDay>();
        foreach (CoreDay activeDay in activeDays)
        {
            bool isDefinedDay = Enum.IsDefined(typeof(CoreDay), activeDay);
            if (isDefinedDay == false || activeDay == CoreDay.None)
            {
                throw new ArgumentException("Schedule summaries cannot contain invalid days.", nameof(activeDays));
            }

            if (uniqueActiveDays.Add(activeDay) == false)
            {
                throw new ArgumentException("Schedule summaries cannot repeat active days.", nameof(activeDays));
            }

            copiedActiveDays.Add(activeDay);
        }

        if (copiedActiveDays.Count == 0)
        {
            throw new ArgumentException("Schedule summaries require at least one active day.", nameof(activeDays));
        }

        SelectedCourseCount = selectedCourseCount;
        ScheduledMeetingCount = scheduledMeetingCount;
        mActiveDays = copiedActiveDays.AsReadOnly();
    }

    private bool hasActiveDay(CoreDay day)
    {
        foreach (CoreDay activeDay in mActiveDays)
        {
            if (activeDay == day)
            {
                return true;
            }
        }

        return false;
    }
}
