namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class EventKitAppleCalendarDescriptorResponse
{
    public string? Identifier { get; set; }

    public string? Name { get; set; }

    public string? SourceIdentifier { get; set; }

    public bool Writable { get; set; }

    public string? RegisteredPlanId { get; set; }

    public string? LegacyPlanId { get; set; }

    public bool LegacyManaged { get; set; }
}
