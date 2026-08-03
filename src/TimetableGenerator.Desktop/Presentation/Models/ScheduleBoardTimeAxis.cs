using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleBoardTimeAxis
{
    private const int EMPTY_FALLBACK_START_MINUTE = 0;
    private const int EMPTY_FALLBACK_END_MINUTE = 60;
    private const int LAYOUT_INCREMENT_MINUTES = 5;
    private const int GUIDE_INTERVAL_MINUTES = 30;
    private const int LABEL_INTERVAL_MINUTES = 60;
    private const int START_CONTEXT_MINUTES = 30;
    private const int MINUTES_PER_HOUR = 60;
    private const int MINUTES_PER_DAY = 1_440;

    private readonly IReadOnlyList<ScheduleBoardTimeBoundary> mGuideTimes;

    private readonly IReadOnlyList<ScheduleBoardTimeBoundary> mLabelTimes;

    public ScheduleBoardTimeBoundary Start { get; }

    public ScheduleBoardTimeBoundary End { get; }

    public IReadOnlyList<ScheduleBoardTimeBoundary> GuideTimes
    {
        get
        {
            return mGuideTimes;
        }
    }

    public IReadOnlyList<ScheduleBoardTimeBoundary> LabelTimes
    {
        get
        {
            return mLabelTimes;
        }
    }

    public int IncrementCount
    {
        get
        {
            return (End.MinutesFromMidnight - Start.MinutesFromMidnight) / LAYOUT_INCREMENT_MINUTES;
        }
    }

    private ScheduleBoardTimeAxis(ScheduleBoardTimeBoundary start, ScheduleBoardTimeBoundary end)
    {
        if (start.CompareTo(end) >= 0)
        {
            throw new ArgumentException("Schedule board time axes must end after they start.", nameof(end));
        }

        Start = start;
        End = end;
        mGuideTimes = createGuideTimes(start, end);
        mLabelTimes = createLabelTimes(start, end);
    }

    public static ScheduleBoardTimeAxis CreateForEntries(IReadOnlyList<ScheduleEntry> entries)
    {
        return createForEntries(entries);
    }

    public static ScheduleBoardTimeAxis CreateForPngExport(IReadOnlyList<ScheduleEntry> entries)
    {
        return createForEntries(entries);
    }

    public int FindStartingRowOffset(ScheduleTime time)
    {
        ensureTimeIsWithinAxis(time);
        int minuteOffset = time.MinutesFromMidnight - Start.MinutesFromMidnight;
        return minuteOffset / LAYOUT_INCREMENT_MINUTES;
    }

    public int FindEndingRowOffset(ScheduleTime time)
    {
        ensureTimeIsWithinAxis(time);
        int minuteOffset = time.MinutesFromMidnight - Start.MinutesFromMidnight;
        return (minuteOffset + LAYOUT_INCREMENT_MINUTES - 1) / LAYOUT_INCREMENT_MINUTES;
    }

    public int FindBoundaryRowOffset(ScheduleBoardTimeBoundary boundary)
    {
        if (boundary.CompareTo(Start) < 0 || boundary.CompareTo(End) > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boundary), boundary, "The time boundary is outside the visible schedule axis.");
        }

        int minuteOffset = boundary.MinutesFromMidnight - Start.MinutesFromMidnight;
        return minuteOffset / LAYOUT_INCREMENT_MINUTES;
    }

    private static ScheduleBoardTimeAxis createForEntries(IReadOnlyList<ScheduleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
        {
            return new ScheduleBoardTimeAxis(new ScheduleBoardTimeBoundary(EMPTY_FALLBACK_START_MINUTE), new ScheduleBoardTimeBoundary(EMPTY_FALLBACK_END_MINUTE));
        }

        int earliestMinute = entries[0].TimeRange.Start.MinutesFromMidnight;
        int latestMinute = entries[0].TimeRange.End.MinutesFromMidnight;
        foreach (ScheduleEntry entry in entries)
        {
            earliestMinute = Math.Min(earliestMinute, entry.TimeRange.Start.MinutesFromMidnight);
            latestMinute = Math.Max(latestMinute, entry.TimeRange.End.MinutesFromMidnight);
        }

        int startMinute = Math.Max(0, roundDown(earliestMinute, MINUTES_PER_HOUR) - START_CONTEXT_MINUTES);
        int endMinute = Math.Min(MINUTES_PER_DAY, roundDown(latestMinute, GUIDE_INTERVAL_MINUTES) + GUIDE_INTERVAL_MINUTES);
        return new ScheduleBoardTimeAxis(new ScheduleBoardTimeBoundary(startMinute), new ScheduleBoardTimeBoundary(endMinute));
    }

    private static IReadOnlyList<ScheduleBoardTimeBoundary> createGuideTimes(ScheduleBoardTimeBoundary start, ScheduleBoardTimeBoundary end)
    {
        List<ScheduleBoardTimeBoundary> guideTimes = new List<ScheduleBoardTimeBoundary>();
        for (int guideMinute = start.MinutesFromMidnight + GUIDE_INTERVAL_MINUTES; guideMinute < end.MinutesFromMidnight; guideMinute += GUIDE_INTERVAL_MINUTES)
        {
            guideTimes.Add(new ScheduleBoardTimeBoundary(guideMinute));
        }

        return guideTimes.AsReadOnly();
    }

    private static IReadOnlyList<ScheduleBoardTimeBoundary> createLabelTimes(ScheduleBoardTimeBoundary start, ScheduleBoardTimeBoundary end)
    {
        List<ScheduleBoardTimeBoundary> labelTimes = new List<ScheduleBoardTimeBoundary>();
        int firstLabelMinute;
        if (start.IsFullHour)
        {
            firstLabelMinute = start.MinutesFromMidnight;
        }
        else
        {
            firstLabelMinute = roundUp(start.MinutesFromMidnight + 1, LABEL_INTERVAL_MINUTES);
        }

        for (int labelMinute = firstLabelMinute; labelMinute < end.MinutesFromMidnight; labelMinute += LABEL_INTERVAL_MINUTES)
        {
            labelTimes.Add(new ScheduleBoardTimeBoundary(labelMinute));
        }

        return labelTimes.AsReadOnly();
    }

    private void ensureTimeIsWithinAxis(ScheduleTime time)
    {
        if (time.IsValid == false
            || time.MinutesFromMidnight < Start.MinutesFromMidnight
            || time.MinutesFromMidnight > End.MinutesFromMidnight)
        {
            throw new ArgumentOutOfRangeException(nameof(time), time, "The schedule time is outside the visible time axis.");
        }
    }

    private static int roundDown(int value, int increment)
    {
        return (value / increment) * increment;
    }

    private static int roundUp(int value, int increment)
    {
        return ((value + increment - 1) / increment) * increment;
    }
}
