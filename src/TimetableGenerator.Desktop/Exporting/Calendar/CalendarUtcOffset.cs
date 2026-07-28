using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal readonly record struct CalendarUtcOffset
{
    private const int MINIMUM_OFFSET_HOURS = -14;
    private const int MAXIMUM_OFFSET_HOURS = 14;

    private readonly bool mIsInitialized;

    public TimeSpan Value { get; }

    public bool IsValid
    {
        get
        {
            return mIsInitialized
                && Value >= TimeSpan.FromHours(MINIMUM_OFFSET_HOURS)
                && Value <= TimeSpan.FromHours(MAXIMUM_OFFSET_HOURS)
                && Value.Ticks % TimeSpan.TicksPerMinute == 0;
        }
    }

    public CalendarUtcOffset(TimeSpan value)
    {
        bool isWithinSupportedRange = value >= TimeSpan.FromHours(MINIMUM_OFFSET_HOURS) && value <= TimeSpan.FromHours(MAXIMUM_OFFSET_HOURS);
        if (isWithinSupportedRange == false || value.Ticks % TimeSpan.TicksPerMinute != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Calendar UTC offsets must use whole minutes from -14:00 through +14:00.");
        }

        Value = value;
        mIsInitialized = true;
    }
}
