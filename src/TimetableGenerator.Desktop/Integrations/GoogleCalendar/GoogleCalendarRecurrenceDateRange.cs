using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed record GoogleCalendarRecurrenceDateRange
{
    public DateOnly FirstOccurrenceDate { get; }

    public DateOnly LastOccurrenceDate { get; }

    public GoogleCalendarRecurrenceDateRange(
        DateOnly firstOccurrenceDate,
        DateOnly lastOccurrenceDate)
    {
        if (firstOccurrenceDate > lastOccurrenceDate)
        {
            throw new ArgumentException(
                "The first event occurrence cannot follow the final occurrence.",
                nameof(lastOccurrenceDate));
        }

        FirstOccurrenceDate = firstOccurrenceDate;
        LastOccurrenceDate = lastOccurrenceDate;
    }
}
