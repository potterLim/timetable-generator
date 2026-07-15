using System;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.UI.Product;

internal sealed class ScheduleSidebarItem
{
    internal ScheduleIndex ScheduleIndex { get; }

    internal ScheduleNumber Number { get; }

    internal ScheduleGridViewModel Schedule { get; }

    internal string Title { get; }

    internal string Summary { get; }

    internal ScheduleSidebarItem(
        ScheduleIndex scheduleIndex,
        ScheduleGridViewModel schedule)
    {
        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
        }

        ScheduleIndex = scheduleIndex;
        Number = ScheduleNumber.FromIndex(scheduleIndex);
        Schedule = schedule;
        Title = "일정 " + Number;
        Summary = ScheduleSummaryTextFormatter.formatSidebarSummary(schedule.Summary);
    }

    public override string ToString()
    {
        return Title + ", " + Summary;
    }
}
