using System.Text.Json.Serialization;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class EventKitAppleCalendarDescriptorResponse
{
    public string? Identifier { get; set; }

    public string? Name { get; set; }

    public string? SourceIdentifier { get; set; }

    [JsonPropertyName("writable")]
    public bool IsWritable { get; set; }

    public string? RegisteredPlanId { get; set; }

    public string? LegacyPlanId { get; set; }

    [JsonPropertyName("legacyManaged")]
    public bool IsLegacyManaged { get; set; }
}
