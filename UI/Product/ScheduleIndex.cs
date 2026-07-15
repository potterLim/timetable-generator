using System;

namespace TimetableGenerator.UI.Product;

public readonly record struct ScheduleIndex
{
    public int Value { get; }

    public bool HasPrevious
    {
        get
        {
            return Value > 0;
        }
    }

    public ScheduleIndex(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Schedule indexes cannot be negative.");
        }

        Value = value;
    }

    public ScheduleIndex GetPrevious()
    {
        if (HasPrevious == false)
        {
            throw new InvalidOperationException("The first schedule does not have a previous schedule.");
        }

        return new ScheduleIndex(Value - 1);
    }

    public ScheduleIndex GetNext()
    {
        if (Value == int.MaxValue)
        {
            throw new InvalidOperationException("The schedule index cannot be incremented.");
        }

        return new ScheduleIndex(Value + 1);
    }
}
