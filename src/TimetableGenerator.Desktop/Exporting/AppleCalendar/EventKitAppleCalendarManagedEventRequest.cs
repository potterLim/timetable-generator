namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class EventKitAppleCalendarManagedEventRequest
{
    public string SourceEventHash { get; }

    public string CalendarItemIdentifier { get; }

    public string ExternalIdentifier { get; }

    public string Fingerprint { get; }

    public EventKitAppleCalendarManagedEventRequest(string sourceEventHash, string calendarItemIdentifier, string externalIdentifier, string fingerprint)
    {
        SourceEventHash = sourceEventHash;
        CalendarItemIdentifier = calendarItemIdentifier;
        ExternalIdentifier = externalIdentifier;
        Fingerprint = fingerprint;
    }
}
