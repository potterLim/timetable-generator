using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class EventKitAppleCalendarRegistrationBindingResponse
{
    public string? PreviousCalendarIdentifier { get; set; }

    public string? CalendarIdentifier { get; set; }

    public string? CalendarName { get; set; }

    public string? SourceIdentifier { get; set; }

    public string? PlanId { get; set; }

    public List<EventKitAppleCalendarEventResponse>? Events { get; set; }
}
