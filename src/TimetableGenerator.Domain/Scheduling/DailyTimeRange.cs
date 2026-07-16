using System;

namespace TimetableGenerator.Domain.Scheduling;

public readonly record struct DailyTimeRange
{
    public ScheduleTime Start { get; }

    public ScheduleTime End { get; }

    public int DurationMinutes
    {
        get
        {
            return End.MinutesFromMidnight - Start.MinutesFromMidnight;
        }
    }

    public bool IsValid
    {
        get
        {
            return Start.IsValid && End.IsValid && Start.CompareTo(End) < 0;
        }
    }

    public DailyTimeRange(ScheduleTime start, ScheduleTime end)
    {
        if (start.IsValid == false)
        {
            throw new ArgumentException(
                "Daily time ranges require a valid start time.",
                nameof(start));
        }

        if (end.IsValid == false)
        {
            throw new ArgumentException(
                "Daily time ranges require a valid end time.",
                nameof(end));
        }

        if (start.CompareTo(end) >= 0)
        {
            throw new ArgumentException(
                "Daily time ranges must end after they start.",
                nameof(end));
        }

        Start = start;
        End = end;
    }

    public override string ToString()
    {
        return Start + "–" + End;
    }
}
