using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Views;

internal sealed class TimePickerMinuteOption
{
    internal const int MINUTES_PER_HOUR = 60;

    internal const int MINUTE_INCREMENT_MINUTES = PersonalSchedule.TIME_INCREMENT_MINUTES;

    public int Value { get; }

    public TimePickerMinuteOption(int value)
    {
        bool usesMinuteIncrement = value % MINUTE_INCREMENT_MINUTES == 0;
        if (value < 0 || value >= MINUTES_PER_HOUR || usesMinuteIncrement == false)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Time picker minutes must use five-minute increments.");
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value.ToString("D2");
    }
}
