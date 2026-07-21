using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class ScheduleBoardTimeAxis
{
    private const int DEFAULT_FIRST_LABEL_MINUTE = 600;
    private const int DEFAULT_END_MINUTE = 1_140;
    private const int PNG_EXPORT_DEFAULT_END_MINUTE = 960;
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
            return (End.MinutesFromMidnight - Start.MinutesFromMidnight)
                / LAYOUT_INCREMENT_MINUTES;
        }
    }

    private ScheduleBoardTimeAxis(
        ScheduleBoardTimeBoundary start,
        ScheduleBoardTimeBoundary end)
    {
        if (start.CompareTo(end) >= 0)
        {
            throw new ArgumentException(
                "Schedule board time axes must end after they start.",
                nameof(end));
        }

        Start = start;
        End = end;
        mGuideTimes = createGuideTimes(start, end);
        mLabelTimes = createLabelTimes(start, end);
    }

    public static ScheduleBoardTimeAxis CreateForEntries(
        IReadOnlyList<ScheduleEntry> entries)
    {
        return createForEntries(entries, DEFAULT_END_MINUTE);
    }

    public static ScheduleBoardTimeAxis CreateForPngExport(
        IReadOnlyList<ScheduleEntry> entries)
    {
        return createForEntries(entries, PNG_EXPORT_DEFAULT_END_MINUTE);
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
        return (minuteOffset + LAYOUT_INCREMENT_MINUTES - 1)
            / LAYOUT_INCREMENT_MINUTES;
    }

    public int FindBoundaryRowOffset(ScheduleBoardTimeBoundary boundary)
    {
        if (boundary.CompareTo(Start) < 0 || boundary.CompareTo(End) > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(boundary),
                boundary,
                "The time boundary is outside the visible schedule axis.");
        }

        int minuteOffset = boundary.MinutesFromMidnight
            - Start.MinutesFromMidnight;
        return minuteOffset / LAYOUT_INCREMENT_MINUTES;
    }

    private static ScheduleBoardTimeAxis createForEntries(
        IReadOnlyList<ScheduleEntry> entries,
        int defaultEndMinute)
    {
        ArgumentNullException.ThrowIfNull(entries);

        int startMinute = findStartMinute(entries);
        int latestMinute = defaultEndMinute;
        foreach (ScheduleEntry entry in entries)
        {
            latestMinute = Math.Max(
                latestMinute,
                entry.TimeRange.End.MinutesFromMidnight);
        }

        int endMinute = Math.Min(
            MINUTES_PER_DAY,
            roundUp(latestMinute, GUIDE_INTERVAL_MINUTES));
        return new ScheduleBoardTimeAxis(
            new ScheduleBoardTimeBoundary(startMinute),
            new ScheduleBoardTimeBoundary(endMinute));
    }

    private static int findStartMinute(IReadOnlyList<ScheduleEntry> entries)
    {
        if (entries.Count == 0)
        {
            return DEFAULT_FIRST_LABEL_MINUTE - START_CONTEXT_MINUTES;
        }

        int earliestMinute = entries[0].TimeRange.Start.MinutesFromMidnight;
        foreach (ScheduleEntry entry in entries)
        {
            earliestMinute = Math.Min(
                earliestMinute,
                entry.TimeRange.Start.MinutesFromMidnight);
        }

        int earliestHourMinute = roundDown(
            earliestMinute,
            MINUTES_PER_HOUR);
        return Math.Max(
            0,
            earliestHourMinute - START_CONTEXT_MINUTES);
    }

    private static IReadOnlyList<ScheduleBoardTimeBoundary> createGuideTimes(
        ScheduleBoardTimeBoundary start,
        ScheduleBoardTimeBoundary end)
    {
        List<ScheduleBoardTimeBoundary> guideTimes =
            new List<ScheduleBoardTimeBoundary>();
        for (int guideMinute =
                start.MinutesFromMidnight + GUIDE_INTERVAL_MINUTES;
            guideMinute < end.MinutesFromMidnight;
            guideMinute += GUIDE_INTERVAL_MINUTES)
        {
            guideTimes.Add(new ScheduleBoardTimeBoundary(guideMinute));
        }

        return guideTimes.AsReadOnly();
    }

    private static IReadOnlyList<ScheduleBoardTimeBoundary> createLabelTimes(
        ScheduleBoardTimeBoundary start,
        ScheduleBoardTimeBoundary end)
    {
        List<ScheduleBoardTimeBoundary> labelTimes =
            new List<ScheduleBoardTimeBoundary>();
        int firstLabelMinute = roundUp(
            start.MinutesFromMidnight + 1,
            LABEL_INTERVAL_MINUTES);
        for (int labelMinute = firstLabelMinute;
            labelMinute < end.MinutesFromMidnight;
            labelMinute += LABEL_INTERVAL_MINUTES)
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
            throw new ArgumentOutOfRangeException(
                nameof(time),
                time,
                "The schedule time is outside the visible time axis.");
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
