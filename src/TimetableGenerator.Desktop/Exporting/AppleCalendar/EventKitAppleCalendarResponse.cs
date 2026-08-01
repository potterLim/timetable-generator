using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class EventKitAppleCalendarResponse
{
    public int SchemaVersion { get; set; }

    public string? Status { get; set; }

    public string? DiagnosticCode { get; set; }

    public List<EventKitAppleCalendarDescriptorResponse>? Calendars { get; set; }

    public List<EventKitAppleCalendarRegistrationBindingResponse>? RegistrationBindings { get; set; }

    public string? CalendarIdentifier { get; set; }

    public string? CalendarName { get; set; }

    public string? SourceIdentifier { get; set; }

    public int CreatedEventCount { get; set; }

    public int DeletedEventCount { get; set; }

    public List<EventKitAppleCalendarEventResponse>? Events { get; set; }
}
