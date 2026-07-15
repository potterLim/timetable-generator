using System;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.UI.Product;

internal sealed class ScheduleSelectionChangedEventArgs : EventArgs
{
    internal ScheduleIndex SelectedIndex { get; }

    internal ScheduleNumber SelectedScheduleNumber { get; }

    internal ScheduleGridViewModel SelectedSchedule { get; }

    internal ScheduleSelectionChangedEventArgs(
        ScheduleIndex selectedIndex,
        ScheduleGridViewModel selectedSchedule)
    {
        if (selectedSchedule == null)
        {
            throw new ArgumentNullException(nameof(selectedSchedule));
        }

        SelectedIndex = selectedIndex;
        SelectedScheduleNumber = ScheduleNumber.FromIndex(selectedIndex);
        SelectedSchedule = selectedSchedule;
    }
}
