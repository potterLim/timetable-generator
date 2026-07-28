using System;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal readonly record struct ScheduleBoardTimeBoundary
    : IComparable<ScheduleBoardTimeBoundary>
{
    private const int MINUTES_PER_HOUR = 60;
    private const int MINUTES_PER_DAY = 1_440;

    public int MinutesFromMidnight { get; }

    public bool IsFullHour
    {
        get
        {
            return MinutesFromMidnight % MINUTES_PER_HOUR == 0;
        }
    }

    public ScheduleBoardTimeBoundary(int minutesFromMidnight)
    {
        if (minutesFromMidnight < 0 || minutesFromMidnight > MINUTES_PER_DAY)
        {
            throw new ArgumentOutOfRangeException(nameof(minutesFromMidnight), minutesFromMidnight, "Schedule board time boundaries must be between 00:00 and 24:00.");
        }

        MinutesFromMidnight = minutesFromMidnight;
    }

    public int CompareTo(ScheduleBoardTimeBoundary other)
    {
        return MinutesFromMidnight.CompareTo(other.MinutesFromMidnight);
    }

    public override string ToString()
    {
        int hour = MinutesFromMidnight / MINUTES_PER_HOUR;
        int minute = MinutesFromMidnight % MINUTES_PER_HOUR;
        return hour.ToString("D2") + ":" + minute.ToString("D2");
    }
}
