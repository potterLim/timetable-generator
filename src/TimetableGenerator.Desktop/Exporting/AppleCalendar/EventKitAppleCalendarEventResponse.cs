namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class EventKitAppleCalendarEventResponse
{
    public string? SourceEventHash { get; set; }

    public string? CalendarItemIdentifier { get; set; }

    public string? ExternalIdentifier { get; set; }

    public string? Fingerprint { get; set; }
}
