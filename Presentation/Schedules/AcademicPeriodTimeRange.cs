using System;
using CorePeriod = TimetableGenerator.Core.Domain.Period;

namespace TimetableGenerator.Presentation.Schedules;

public readonly record struct AcademicPeriodTimeRange
{
    public CorePeriod Period { get; }

    public TimeOnly StartTime { get; }

    public TimeOnly EndTime { get; }

    public TimeSpan Duration
    {
        get
        {
            return EndTime - StartTime;
        }
    }

    public bool IsValid
    {
        get
        {
            return Period.IsValid && EndTime > StartTime;
        }
    }

    internal AcademicPeriodTimeRange(
        CorePeriod period,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        if (period.IsValid == false)
        {
            throw new ArgumentException("Academic time ranges require a valid period.", nameof(period));
        }

        if (endTime <= startTime)
        {
            throw new ArgumentException("Academic periods must end later on the same day they start.", nameof(endTime));
        }

        Period = period;
        StartTime = startTime;
        EndTime = endTime;
    }
}
