using System;
using System.Globalization;

namespace TimetableGenerator.UI.Product;

public readonly record struct ScheduleNumber
{
    public int Value { get; }

    public bool IsValid
    {
        get
        {
            return Value > 0;
        }
    }

    public ScheduleNumber(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Schedule numbers must be greater than zero.");
        }

        Value = value;
    }

    public static ScheduleNumber FromIndex(ScheduleIndex scheduleIndex)
    {
        if (scheduleIndex.Value == int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleIndex));
        }

        return new ScheduleNumber(scheduleIndex.Value + 1);
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
