using System;

namespace TimetableGenerator.Domain.Scheduling;

public readonly record struct ScheduleTime : IComparable<ScheduleTime>
{
    private const int MINUTES_PER_HOUR = 60;
    private const int MINUTES_PER_DAY = 1440;

    public int MinutesFromMidnight { get; }

    public int Hour
    {
        get
        {
            return MinutesFromMidnight / MINUTES_PER_HOUR;
        }
    }

    public int Minute
    {
        get
        {
            return MinutesFromMidnight % MINUTES_PER_HOUR;
        }
    }

    public bool IsValid
    {
        get
        {
            return MinutesFromMidnight >= 0 && MinutesFromMidnight < MINUTES_PER_DAY;
        }
    }

    public ScheduleTime(int hour, int minute)
    {
        if (hour < 0 || hour > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), hour, "Schedule hours must be between 0 and 23.");
        }

        if (minute < 0 || minute > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), minute, "Schedule minutes must be between 0 and 59.");
        }

        MinutesFromMidnight = (hour * MINUTES_PER_HOUR) + minute;
    }

    public int CompareTo(ScheduleTime other)
    {
        return MinutesFromMidnight.CompareTo(other.MinutesFromMidnight);
    }

    public override string ToString()
    {
        return Hour.ToString("D2") + ":" + Minute.ToString("D2");
    }
}
