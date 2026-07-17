using System;

namespace TimetableGenerator.Desktop.Views;

internal sealed class TimePickerHourOption
{
    internal const int MINIMUM_VALUE = 1;

    internal const int MAXIMUM_VALUE = 12;

    public int Value { get; }

    public TimePickerHourOption(int value)
    {
        if (value < MINIMUM_VALUE || value > MAXIMUM_VALUE)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Time picker hours must be between 1 and 12.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString("D2");
    }
}
