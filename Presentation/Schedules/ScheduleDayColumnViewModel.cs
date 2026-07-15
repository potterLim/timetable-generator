using System;
using CoreDay = TimetableGenerator.Core.Domain.EDay;

namespace TimetableGenerator.Presentation.Schedules;

public sealed class ScheduleDayColumnViewModel
{
    public CoreDay Day { get; }

    public string DisplayName { get; }

    internal ScheduleDayColumnViewModel(CoreDay day, string displayName)
    {
        bool isDefinedDay = Enum.IsDefined(typeof(CoreDay), day);
        if (isDefinedDay == false || day == CoreDay.None)
        {
            throw new ArgumentOutOfRangeException(nameof(day));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Schedule day display names cannot be empty.", nameof(displayName));
        }

        Day = day;
        DisplayName = displayName;
    }
}
