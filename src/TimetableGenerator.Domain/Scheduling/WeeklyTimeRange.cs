using System;

namespace TimetableGenerator.Domain.Scheduling;

public readonly record struct WeeklyTimeRange
{
    public EDay Day { get; }

    public DailyTimeRange TimeRange { get; }

    public bool IsValid
    {
        get
        {
            bool isDefinedDay = Enum.IsDefined(typeof(EDay), Day);
            return isDefinedDay && Day != EDay.None && TimeRange.IsValid;
        }
    }

    public WeeklyTimeRange(EDay day, DailyTimeRange timeRange)
    {
        bool isDefinedDay = Enum.IsDefined(typeof(EDay), day);
        if (isDefinedDay == false || day == EDay.None)
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "Weekly time ranges require a defined day.");
        }

        if (timeRange.IsValid == false)
        {
            throw new ArgumentException("Weekly time ranges require a valid daily range.", nameof(timeRange));
        }

        Day = day;
        TimeRange = timeRange;
    }

    public override string ToString()
    {
        return Day + ":" + TimeRange;
    }
}
