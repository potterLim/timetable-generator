using System;
using System.Collections.Generic;
using CoreDay = TimetableGenerator.Core.Domain.EDay;
using CorePeriod = TimetableGenerator.Core.Domain.Period;

namespace TimetableGenerator.Presentation.Schedules;

public sealed class ScheduleGridViewModel
{
    private readonly IReadOnlyList<ScheduleDayColumnViewModel> mDayColumns;
    private readonly IReadOnlyList<SchedulePeriodRowViewModel> mPeriodRows;

    public IReadOnlyList<ScheduleDayColumnViewModel> DayColumns
    {
        get
        {
            return mDayColumns;
        }
    }

    public IReadOnlyList<SchedulePeriodRowViewModel> PeriodRows
    {
        get
        {
            return mPeriodRows;
        }
    }

    public CorePeriod MaximumVisiblePeriod
    {
        get
        {
            return mPeriodRows[mPeriodRows.Count - 1].Period;
        }
    }

    public ScheduleGridSummary Summary { get; }

    internal ScheduleGridViewModel(
        IEnumerable<ScheduleDayColumnViewModel> dayColumns,
        IEnumerable<SchedulePeriodRowViewModel> periodRows,
        ScheduleGridSummary summary)
    {
        if (dayColumns == null)
        {
            throw new ArgumentNullException(nameof(dayColumns));
        }

        if (periodRows == null)
        {
            throw new ArgumentNullException(nameof(periodRows));
        }

        if (summary == null)
        {
            throw new ArgumentNullException(nameof(summary));
        }

        List<ScheduleDayColumnViewModel> copiedDayColumns = copyDayColumns(dayColumns);
        List<SchedulePeriodRowViewModel> copiedPeriodRows = copyAndValidatePeriodRows(
            periodRows,
            copiedDayColumns);

        mDayColumns = copiedDayColumns.AsReadOnly();
        mPeriodRows = copiedPeriodRows.AsReadOnly();
        Summary = summary;
    }

    public SchedulePeriodRowViewModel GetRowByPeriod(CorePeriod period)
    {
        if (period.IsValid == false)
        {
            throw new ArgumentException("A valid period is required.", nameof(period));
        }

        foreach (SchedulePeriodRowViewModel periodRow in mPeriodRows)
        {
            if (periodRow.Period == period)
            {
                return periodRow;
            }
        }

        throw new KeyNotFoundException("The requested period is not visible in this schedule grid.");
    }

    public ScheduleCellViewModel GetCell(CoreDay day, CorePeriod period)
    {
        SchedulePeriodRowViewModel periodRow = GetRowByPeriod(period);
        return periodRow.GetCellByDay(day);
    }

    private static List<ScheduleDayColumnViewModel> copyDayColumns(
        IEnumerable<ScheduleDayColumnViewModel> dayColumns)
    {
        List<ScheduleDayColumnViewModel> copiedDayColumns = new List<ScheduleDayColumnViewModel>();
        HashSet<CoreDay> uniqueDays = new HashSet<CoreDay>();

        foreach (ScheduleDayColumnViewModel dayColumn in dayColumns)
        {
            if (dayColumn == null)
            {
                throw new ArgumentException("Schedule grids cannot contain null day columns.", nameof(dayColumns));
            }

            if (uniqueDays.Add(dayColumn.Day) == false)
            {
                throw new ArgumentException("Schedule grids cannot contain duplicate day columns.", nameof(dayColumns));
            }

            copiedDayColumns.Add(dayColumn);
        }

        if (copiedDayColumns.Count == 0)
        {
            throw new ArgumentException("Schedule grids require at least one day column.", nameof(dayColumns));
        }

        return copiedDayColumns;
    }

    private static List<SchedulePeriodRowViewModel> copyAndValidatePeriodRows(
        IEnumerable<SchedulePeriodRowViewModel> periodRows,
        IReadOnlyList<ScheduleDayColumnViewModel> dayColumns)
    {
        List<SchedulePeriodRowViewModel> copiedPeriodRows = new List<SchedulePeriodRowViewModel>();
        int expectedPeriodValue = 1;

        foreach (SchedulePeriodRowViewModel periodRow in periodRows)
        {
            if (periodRow == null)
            {
                throw new ArgumentException("Schedule grids cannot contain null period rows.", nameof(periodRows));
            }

            if (periodRow.Period.Value != expectedPeriodValue)
            {
                throw new ArgumentException("Schedule grid period rows must be contiguous and one-based.", nameof(periodRows));
            }

            if (periodRow.Cells.Count != dayColumns.Count)
            {
                throw new ArgumentException("Every schedule row must contain one cell per day column.", nameof(periodRows));
            }

            for (int columnIndex = 0; columnIndex < dayColumns.Count; ++columnIndex)
            {
                if (periodRow.Cells[columnIndex].ScheduleSlot.Day != dayColumns[columnIndex].Day)
                {
                    throw new ArgumentException("Schedule row cells must follow the day column order.", nameof(periodRows));
                }
            }

            copiedPeriodRows.Add(periodRow);
            ++expectedPeriodValue;
        }

        if (copiedPeriodRows.Count == 0)
        {
            throw new ArgumentException("Schedule grids require at least one period row.", nameof(periodRows));
        }

        return copiedPeriodRows;
    }
}
