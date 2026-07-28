using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal readonly record struct GoogleCalendarReconciliationResult
{
    public int CreatedEventCount { get; }

    public int UpdatedEventCount { get; }

    public int DeletedEventCount { get; }

    public GoogleCalendarReconciliationResult(int createdEventCount, int updatedEventCount, int deletedEventCount)
    {
        if (createdEventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(createdEventCount));
        }

        if (updatedEventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(updatedEventCount));
        }

        if (deletedEventCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deletedEventCount));
        }

        CreatedEventCount = createdEventCount;
        UpdatedEventCount = updatedEventCount;
        DeletedEventCount = deletedEventCount;
    }
}
