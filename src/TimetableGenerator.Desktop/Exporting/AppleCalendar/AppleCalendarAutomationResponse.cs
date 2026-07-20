using System.Collections.Generic;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarAutomationResponse
{
    public string? Status { get; set; }

    public string? DiagnosticCode { get; set; }

    public List<AppleCalendarAutomationCalendarResponse>? Calendars { get; set; }

    public string? CalendarId { get; set; }

    public string? CalendarName { get; set; }

    public int CreatedEventCount { get; set; }

    public int DeletedEventCount { get; set; }
}

internal sealed class AppleCalendarAutomationCalendarResponse
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public bool Writable { get; set; }
}
