using System;
using System.Collections.Generic;
using CoreCourseOffering = TimetableGenerator.Core.Domain.CourseOffering;
using CoreDay = TimetableGenerator.Core.Domain.EDay;
using CoreGeneratedSchedule = TimetableGenerator.Core.Domain.GeneratedSchedule;
using CorePeriod = TimetableGenerator.Core.Domain.Period;
using CoreScheduleSlot = TimetableGenerator.Core.Domain.ScheduleSlot;

namespace TimetableGenerator.Presentation.Schedules;

public static class ScheduleGridViewModelFactory
{
    private const int DEFAULT_VISIBLE_PERIOD_VALUE = 8;

    private static readonly CoreDay[] DAY_ORDER = new CoreDay[]
    {
        CoreDay.Monday,
        CoreDay.Tuesday,
        CoreDay.Wednesday,
        CoreDay.Thursday,
        CoreDay.Friday,
        CoreDay.Saturday,
        CoreDay.Sunday,
    };

    public static ScheduleGridViewModel Create(CoreGeneratedSchedule generatedSchedule)
    {
        if (generatedSchedule == null)
        {
            throw new ArgumentNullException(nameof(generatedSchedule));
        }

        Dictionary<CoreScheduleSlot, CoreCourseOffering> courseOfferingByScheduleSlot =
            new Dictionary<CoreScheduleSlot, CoreCourseOffering>();
        HashSet<CoreDay> activeDays = new HashSet<CoreDay>();
        int maximumVisiblePeriodValue = DEFAULT_VISIBLE_PERIOD_VALUE;
        int scheduledMeetingCount = 0;

        foreach (CoreCourseOffering courseOffering in generatedSchedule.CourseOfferings)
        {
            foreach (CoreScheduleSlot scheduleSlot in courseOffering.ScheduleSlots)
            {
                AcademicPeriodTimePolicy.GetTimeRange(scheduleSlot.Period);

                if (courseOfferingByScheduleSlot.TryAdd(scheduleSlot, courseOffering) == false)
                {
                    throw new ArgumentException(
                        "Generated schedules cannot contain overlapping schedule slots.",
                        nameof(generatedSchedule));
                }

                activeDays.Add(scheduleSlot.Day);
                if (scheduleSlot.Period.Value > maximumVisiblePeriodValue)
                {
                    maximumVisiblePeriodValue = scheduleSlot.Period.Value;
                }

                ++scheduledMeetingCount;
            }
        }

        IReadOnlyList<CoreDay> orderedActiveDays = getOrderedActiveDays(activeDays);
        IReadOnlyList<ScheduleDayColumnViewModel> dayColumns = createDayColumns(activeDays);
        IReadOnlyList<SchedulePeriodRowViewModel> periodRows = createPeriodRows(
            dayColumns,
            maximumVisiblePeriodValue,
            courseOfferingByScheduleSlot);
        ScheduleGridSummary summary = new ScheduleGridSummary(
            generatedSchedule.CourseOfferings.Count,
            scheduledMeetingCount,
            orderedActiveDays);

        return new ScheduleGridViewModel(dayColumns, periodRows, summary);
    }

    private static IReadOnlyList<CoreDay> getOrderedActiveDays(ISet<CoreDay> activeDays)
    {
        List<CoreDay> orderedActiveDays = new List<CoreDay>();
        foreach (CoreDay day in DAY_ORDER)
        {
            if (activeDays.Contains(day))
            {
                orderedActiveDays.Add(day);
            }
        }

        return orderedActiveDays.AsReadOnly();
    }

    private static IReadOnlyList<ScheduleDayColumnViewModel> createDayColumns(
        ISet<CoreDay> activeDays)
    {
        List<ScheduleDayColumnViewModel> dayColumns = new List<ScheduleDayColumnViewModel>();

        foreach (CoreDay day in DAY_ORDER)
        {
            bool isWeekday = day >= CoreDay.Monday && day <= CoreDay.Friday;
            if (isWeekday == false && activeDays.Contains(day) == false)
            {
                continue;
            }

            ScheduleDayColumnViewModel dayColumn = new ScheduleDayColumnViewModel(
                day,
                getDayDisplayName(day));
            dayColumns.Add(dayColumn);
        }

        return dayColumns.AsReadOnly();
    }

    private static IReadOnlyList<SchedulePeriodRowViewModel> createPeriodRows(
        IReadOnlyList<ScheduleDayColumnViewModel> dayColumns,
        int maximumVisiblePeriodValue,
        IReadOnlyDictionary<CoreScheduleSlot, CoreCourseOffering> courseOfferingByScheduleSlot)
    {
        List<SchedulePeriodRowViewModel> periodRows = new List<SchedulePeriodRowViewModel>(
            maximumVisiblePeriodValue);

        for (int periodValue = 1; periodValue <= maximumVisiblePeriodValue; ++periodValue)
        {
            CorePeriod period = new CorePeriod(periodValue);
            AcademicPeriodTimeRange timeRange = AcademicPeriodTimePolicy.GetTimeRange(period);
            List<ScheduleCellViewModel> cells = new List<ScheduleCellViewModel>(dayColumns.Count);

            foreach (ScheduleDayColumnViewModel dayColumn in dayColumns)
            {
                CoreScheduleSlot scheduleSlot = new CoreScheduleSlot(dayColumn.Day, period);
                CoreCourseOffering courseOfferingOrNull;
                bool hasCourseOffering = courseOfferingByScheduleSlot.TryGetValue(
                    scheduleSlot,
                    out courseOfferingOrNull);
                ScheduleCellViewModel cell;
                if (hasCourseOffering)
                {
                    cell = ScheduleCellViewModel.createScheduled(scheduleSlot, courseOfferingOrNull);
                }
                else
                {
                    cell = ScheduleCellViewModel.createEmpty(scheduleSlot);
                }

                cells.Add(cell);
            }

            SchedulePeriodRowViewModel periodRow = new SchedulePeriodRowViewModel(
                period,
                timeRange,
                cells);
            periodRows.Add(periodRow);
        }

        return periodRows.AsReadOnly();
    }

    private static string getDayDisplayName(CoreDay day)
    {
        switch (day)
        {
            case CoreDay.Monday:
                return "월";
            case CoreDay.Tuesday:
                return "화";
            case CoreDay.Wednesday:
                return "수";
            case CoreDay.Thursday:
                return "목";
            case CoreDay.Friday:
                return "금";
            case CoreDay.Saturday:
                return "토";
            case CoreDay.Sunday:
                return "일";
            case CoreDay.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(day));
        }
    }
}
