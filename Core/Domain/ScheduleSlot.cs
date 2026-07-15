using System;

namespace TimetableGenerator.Core.Domain;

public readonly record struct ScheduleSlot
{
    public EDay Day { get; }

    public Period Period { get; }

    public bool IsValid
    {
        get
        {
            bool isDefinedDay = Enum.IsDefined(typeof(EDay), Day);
            return isDefinedDay && Day != EDay.None && Period.IsValid;
        }
    }

    public ScheduleSlot(EDay day, Period period)
    {
        bool isDefinedDay = Enum.IsDefined(typeof(EDay), day);
        if (isDefinedDay == false || day == EDay.None)
        {
            throw new ArgumentOutOfRangeException(nameof(day), "Schedule slots require a defined day.");
        }

        if (period.IsValid == false)
        {
            throw new ArgumentException("Schedule slots require a valid period.", nameof(period));
        }

        Day = day;
        Period = period;
    }

    public override string ToString()
    {
        return Day + ":" + Period;
    }
}
