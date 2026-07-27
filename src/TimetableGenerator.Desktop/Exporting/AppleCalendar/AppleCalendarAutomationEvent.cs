using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarAutomationEvent
{
    public string EventId { get; }

    public string OwnershipUrl { get; }

    public string Summary { get; }

    public string Location { get; }

    public string Description { get; }

    public string StartsAt { get; }

    public string EndsAt { get; }

    public AppleCalendarAutomationEvent(
        PlanId planId,
        string eventId,
        string summary,
        string location,
        string description,
        string startsAt,
        string endsAt)
    {
        EventId = requireText(eventId, nameof(eventId));
        OwnershipUrl = AppleCalendarEventOwnershipMarker.Create(planId, EventId);
        Summary = requireText(summary, nameof(summary));
        Location = requireValue(location, nameof(location));
        Description = requireValue(description, nameof(description));
        StartsAt = requireText(startsAt, nameof(startsAt));
        EndsAt = requireText(endsAt, nameof(endsAt));
    }

    private static string requireText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Apple Calendar event values cannot be empty.", parameterName);
        }

        return value.Trim();
    }

    private static string requireValue(string value, string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        return value.Trim();
    }
}
