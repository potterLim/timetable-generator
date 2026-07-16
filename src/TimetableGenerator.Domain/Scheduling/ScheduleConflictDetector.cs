using System;

namespace TimetableGenerator.Domain.Scheduling;

public static class ScheduleConflictDetector
{
    public static bool HasConflict(
        WeeklyTimeRange firstRange,
        WeeklyTimeRange secondRange)
    {
        if (firstRange.IsValid == false)
        {
            throw new ArgumentException(
                "Conflict checks require a valid first weekly time range.",
                nameof(firstRange));
        }

        if (secondRange.IsValid == false)
        {
            throw new ArgumentException(
                "Conflict checks require a valid second weekly time range.",
                nameof(secondRange));
        }

        if (firstRange.Day != secondRange.Day)
        {
            return false;
        }

        return firstRange.TimeRange.Start.CompareTo(secondRange.TimeRange.End) < 0
            && secondRange.TimeRange.Start.CompareTo(firstRange.TimeRange.End) < 0;
    }

    public static bool HasConflict(
        ScheduledOffering firstOffering,
        ScheduledOffering secondOffering)
    {
        if (firstOffering == null)
        {
            throw new ArgumentNullException(nameof(firstOffering));
        }

        if (secondOffering == null)
        {
            throw new ArgumentNullException(nameof(secondOffering));
        }

        foreach (MeetingSlot firstSlot in firstOffering.MeetingSlots)
        {
            foreach (MeetingSlot secondSlot in secondOffering.MeetingSlots)
            {
                if (firstSlot == secondSlot)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
