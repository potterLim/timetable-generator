using System.Collections.Generic;
using System.Text.Json;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed partial class PlanningWorkspaceJsonCodec
{
    private static void writeLastViewedRecommendation(
        Utf8JsonWriter writer,
        ScheduleRecommendationBookmark? recommendationBookmarkOrNull)
    {
        if (recommendationBookmarkOrNull == null)
        {
            writer.WriteNull("lastViewedRecommendation");
            return;
        }

        writer.WriteStartObject("lastViewedRecommendation");
        writer.WriteStartArray("scheduledOfferingIds");
        foreach (OfferingId offeringId
            in recommendationBookmarkOrNull.SelectedOfferingIds)
        {
            writer.WriteStringValue(offeringId.Value);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static ScheduleRecommendationBookmark? readLastViewedRecommendationOrNull(
        JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        Dictionary<string, JsonElement> properties = readExactObject(
            element,
            "plan.lastViewedRecommendation",
            new string[] { "scheduledOfferingIds" });
        JsonElement offeringIdsElement = properties["scheduledOfferingIds"];
        requireValueKind(
            offeringIdsElement,
            JsonValueKind.Array,
            "plan.lastViewedRecommendation.scheduledOfferingIds");
        List<OfferingId> offeringIds = new List<OfferingId>();
        foreach (JsonElement offeringIdElement
            in offeringIdsElement.EnumerateArray())
        {
            string offeringIdValue = readString(
                offeringIdElement,
                "plan.lastViewedRecommendation.scheduledOfferingIds[]");
            offeringIds.Add(new OfferingId(offeringIdValue));
        }

        return new ScheduleRecommendationBookmark(offeringIds);
    }
}
