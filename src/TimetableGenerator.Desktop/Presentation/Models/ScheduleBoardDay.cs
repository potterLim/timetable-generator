using System;

using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal readonly record struct ScheduleBoardDay
{
    public EDay Day { get; }

    public int ColumnIndex { get; }

    public string ShortDisplayName { get; }

    public string FullDisplayName { get; }

    public ScheduleBoardDay(
        EDay day,
        int columnIndex,
        string shortDisplayName,
        string fullDisplayName)
    {
        ensureDefinedDay(day);
        if (columnIndex < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columnIndex),
                columnIndex,
                "Schedule day columns must follow the time column.");
        }

        if (string.IsNullOrWhiteSpace(shortDisplayName))
        {
            throw new ArgumentException(
                "Schedule days require a short display name.",
                nameof(shortDisplayName));
        }

        if (string.IsNullOrWhiteSpace(fullDisplayName))
        {
            throw new ArgumentException(
                "Schedule days require a full display name.",
                nameof(fullDisplayName));
        }

        Day = day;
        ColumnIndex = columnIndex;
        ShortDisplayName = shortDisplayName;
        FullDisplayName = fullDisplayName;
    }

    private static void ensureDefinedDay(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
            case EDay.Tuesday:
            case EDay.Wednesday:
            case EDay.Thursday:
            case EDay.Friday:
            case EDay.Saturday:
            case EDay.Sunday:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Schedule board days require a defined day of the week.");
        }
    }
}
