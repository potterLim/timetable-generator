using System;
using System.Collections.Generic;
using CoreDay = TimetableGenerator.Core.Domain.EDay;
using CorePeriod = TimetableGenerator.Core.Domain.Period;

namespace TimetableGenerator.Presentation.Schedules;

public sealed class SchedulePeriodRowViewModel
{
    public CorePeriod Period { get; }

    public AcademicPeriodTimeRange TimeRange { get; }

    private readonly IReadOnlyList<ScheduleCellViewModel> mCells;

    public IReadOnlyList<ScheduleCellViewModel> Cells
    {
        get
        {
            return mCells;
        }
    }

    internal SchedulePeriodRowViewModel(
        CorePeriod period,
        AcademicPeriodTimeRange timeRange,
        IEnumerable<ScheduleCellViewModel> cells)
    {
        if (period.IsValid == false)
        {
            throw new ArgumentException("Schedule rows require a valid period.", nameof(period));
        }

        if (timeRange.IsValid == false || timeRange.Period != period)
        {
            throw new ArgumentException("The row time range must match its period.", nameof(timeRange));
        }

        if (cells == null)
        {
            throw new ArgumentNullException(nameof(cells));
        }

        List<ScheduleCellViewModel> copiedCells = new List<ScheduleCellViewModel>();
        HashSet<CoreDay> cellDays = new HashSet<CoreDay>();
        foreach (ScheduleCellViewModel cell in cells)
        {
            if (cell == null)
            {
                throw new ArgumentException("Schedule rows cannot contain null cells.", nameof(cells));
            }

            if (cell.ScheduleSlot.Period != period)
            {
                throw new ArgumentException("Every schedule cell must match its row period.", nameof(cells));
            }

            if (cellDays.Add(cell.ScheduleSlot.Day) == false)
            {
                throw new ArgumentException("Schedule rows cannot contain duplicate day cells.", nameof(cells));
            }

            copiedCells.Add(cell);
        }

        if (copiedCells.Count == 0)
        {
            throw new ArgumentException("Schedule rows require at least one day cell.", nameof(cells));
        }

        Period = period;
        TimeRange = timeRange;
        mCells = copiedCells.AsReadOnly();
    }

    public ScheduleCellViewModel GetCellByDay(CoreDay day)
    {
        foreach (ScheduleCellViewModel cell in mCells)
        {
            if (cell.ScheduleSlot.Day == day)
            {
                return cell;
            }
        }

        throw new KeyNotFoundException("The requested day is not visible in this schedule row.");
    }
}
