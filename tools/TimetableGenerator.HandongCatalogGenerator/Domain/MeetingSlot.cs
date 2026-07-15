using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal readonly record struct MeetingSlot
{
    public EDay Day { get; }

    public AcademicPeriod Period { get; }

    public MeetingSlot(EDay day, AcademicPeriod period)
    {
        if (Enum.IsDefined(typeof(EDay), day) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(day));
        }

        if (period.Value <= 0)
        {
            throw new ArgumentException("Meeting slots require a valid academic period.", nameof(period));
        }

        Day = day;
        Period = period;
    }
}
