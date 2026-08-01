namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarManagedEventRegistration
{
    public string SourceEventHash { get; }

    public string CalendarItemIdentifier { get; }

    public string? ExternalIdentifierOrNull { get; }

    public string Fingerprint { get; }

    public AppleCalendarManagedEventRegistration(string sourceEventHash, string calendarItemIdentifier, string? externalIdentifierOrNull, string fingerprint)
    {
        SourceEventHash = AppleCalendarRegistryValue.RequireHash(sourceEventHash, nameof(sourceEventHash));
        CalendarItemIdentifier = AppleCalendarRegistryValue.RequireText(calendarItemIdentifier, nameof(calendarItemIdentifier));
        ExternalIdentifierOrNull = AppleCalendarRegistryValue.NormalizeOptionalText(externalIdentifierOrNull);
        Fingerprint = AppleCalendarRegistryValue.RequireHash(fingerprint, nameof(fingerprint));
    }
}
