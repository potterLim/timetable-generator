namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarPendingEvent
{
    public string SourceEventHash { get; }

    public string Fingerprint { get; }

    public AppleCalendarPendingEvent(string sourceEventHash, string fingerprint)
    {
        SourceEventHash = AppleCalendarRegistryValue.RequireHash(sourceEventHash, nameof(sourceEventHash));
        Fingerprint = AppleCalendarRegistryValue.RequireHash(fingerprint, nameof(fingerprint));
    }
}
