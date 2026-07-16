using System;

namespace TimetableGenerator.Domain.Scheduling;

public readonly record struct MeetingSlot
{
    public EDay Day { get; }

    public AcademicPeriod Period { get; }

    public bool IsValid
    {
        get
        {
            bool isDefinedDay = Enum.IsDefined(typeof(EDay), Day);
            return isDefinedDay && Day != EDay.None && Period.IsValid;
        }
    }

    public MeetingSlot(EDay day, AcademicPeriod period)
    {
        bool isDefinedDay = Enum.IsDefined(typeof(EDay), day);
        if (isDefinedDay == false || day == EDay.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(day),
                day,
                "Meeting slots require a defined day.");
        }

        if (period.IsValid == false)
        {
            throw new ArgumentException(
                "Meeting slots require a valid academic period.",
                nameof(period));
        }

        Day = day;
        Period = period;
    }

    public override string ToString()
    {
        return Day + ":" + Period;
    }
}
