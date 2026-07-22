using System;
using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleBoardLayout
{
    private static readonly ScheduleBoardLayout DEFAULT_LAYOUT = CreateForEntries(Array.Empty<ScheduleEntry>());

    public static ScheduleBoardLayout Default
    {
        get
        {
            return DEFAULT_LAYOUT;
        }
    }

    public ScheduleBoardDayRange DayRange { get; }

    public ScheduleBoardTimeAxis TimeAxis { get; }

    private ScheduleBoardLayout(ScheduleBoardDayRange dayRange, ScheduleBoardTimeAxis timeAxis)
    {
        DayRange = dayRange;
        TimeAxis = timeAxis;
    }

    public static ScheduleBoardLayout CreateForEntries(IReadOnlyList<ScheduleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return CreateForEntries(entries, ScheduleBoardDayRange.CreateForEntries(entries));
    }

    public static ScheduleBoardLayout CreateForEntries(
        IReadOnlyList<ScheduleEntry> entries,
        ScheduleBoardDayRange dayRange)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(dayRange);
        return new ScheduleBoardLayout(dayRange, ScheduleBoardTimeAxis.CreateForEntries(entries));
    }

    public static ScheduleBoardLayout CreateForPngExport(IReadOnlyList<ScheduleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return new ScheduleBoardLayout(
            ScheduleBoardDayRange.CreateForEntries(entries),
            ScheduleBoardTimeAxis.CreateForPngExport(entries));
    }
}
