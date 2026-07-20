using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed partial class PlanningWorkspaceJsonCodec
{
    private const int CURRENT_SCHEMA_VERSION = 5;
    private const int RECOMMENDATION_BOOKMARK_SCHEMA_VERSION = 4;
    private const int COURSE_CHOICE_GROUP_SCHEMA_VERSION = 3;
    private const int PERSONAL_SCHEDULE_SCHEMA_VERSION = 2;
    private const int LEGACY_SCHEMA_VERSION = 1;

    public byte[] Serialize(PlanningWorkspaceDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>();
        JsonWriterOptions options = default(JsonWriterOptions);
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.Indented = true;
        using (Utf8JsonWriter writer = new Utf8JsonWriter(buffer, options))
        {
            writeWorkspace(writer, document);
            writer.Flush();
        }

        return buffer.WrittenSpan.ToArray();
    }

    public PlanningWorkspaceDocument Deserialize(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty)
        {
            throw new WorkspaceDocumentException(
                "The planning workspace document is empty.");
        }

        try
        {
            using (JsonDocument document = JsonDocument.Parse(content))
            {
                return readWorkspace(document.RootElement);
            }
        }
        catch (JsonException exception)
        {
            throw new WorkspaceDocumentException(
                "The planning workspace document is not valid JSON.",
                exception);
        }
        catch (ArgumentException exception)
        {
            throw new WorkspaceDocumentException(
                "The planning workspace document violates product invariants.",
                exception);
        }
        catch (FormatException exception)
        {
            throw new WorkspaceDocumentException(
                "The planning workspace document contains an invalid formatted value.",
                exception);
        }
    }

    private static void writeWorkspace(
        Utf8JsonWriter writer,
        PlanningWorkspaceDocument document)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", CURRENT_SCHEMA_VERSION);
        writer.WriteNumber("generation", document.Generation.Value);
        PlanningWorkspace workspace = document.Workspace;
        writeCatalogBinding(writer, "catalog", workspace.CatalogBinding);
        writeOptionalPlanId(writer, "activePlanId", workspace.ActivePlanIdOrNull);
        writer.WriteStartArray("plans");
        foreach (PlanningPlan plan in workspace.Plans)
        {
            writePlan(writer, plan);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void writePlan(Utf8JsonWriter writer, PlanningPlan plan)
    {
        writer.WriteStartObject();
        writer.WriteString("id", plan.Id.ToString());
        writer.WriteString("name", plan.Name.Value);
        writeCatalogBinding(writer, "catalog", plan.CatalogBinding);
        writer.WriteStartArray("courseChoiceGroups");
        foreach (CourseChoiceGroup courseChoiceGroup in plan.CourseChoiceGroups)
        {
            writeCourseChoiceGroup(writer, courseChoiceGroup);
        }

        writer.WriteEndArray();
        writer.WriteStartArray("unscheduledSelections");
        foreach (UnscheduledOfferingSelection selection in
            plan.UnscheduledOfferingSelections)
        {
            writer.WriteStartObject();
            writer.WriteString("courseId", selection.CourseId.Value);
            writer.WriteString("offeringId", selection.OfferingId.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("personalSchedules");
        foreach (PersonalSchedule personalSchedule in plan.PersonalSchedules)
        {
            writePersonalSchedule(writer, personalSchedule);
        }

        writer.WriteEndArray();
        writeLastViewedRecommendation(
            writer,
            plan.LastViewedRecommendationOrNull);
        writer.WriteEndObject();
    }

    private static PlanningWorkspaceDocument readWorkspace(JsonElement element)
    {
        int schemaVersion = readSchemaVersion(element);
        switch (schemaVersion)
        {
            case LEGACY_SCHEMA_VERSION:
            case PERSONAL_SCHEDULE_SCHEMA_VERSION:
            case COURSE_CHOICE_GROUP_SCHEMA_VERSION:
            case RECOMMENDATION_BOOKMARK_SCHEMA_VERSION:
            case CURRENT_SCHEMA_VERSION:
                return readWorkspaceVersion(element, schemaVersion);
            default:
                throw new UnsupportedWorkspaceSchemaVersionException(schemaVersion);
        }
    }

    private static PlanningWorkspaceDocument readWorkspaceVersion(
        JsonElement element,
        int schemaVersion)
    {
        IReadOnlyList<string> expectedPropertyNames;
        if (schemaVersion == CURRENT_SCHEMA_VERSION)
        {
            expectedPropertyNames = new string[]
            {
                "schemaVersion",
                "generation",
                "catalog",
                "activePlanId",
                "plans",
            };
        }
        else
        {
            expectedPropertyNames = new string[]
            {
                "schemaVersion",
                "generation",
                "activePlanId",
                "plans",
            };
        }

        Dictionary<string, JsonElement> properties = readExactObject(
            element,
            "workspace",
            expectedPropertyNames);
        WorkspaceGeneration generation = new WorkspaceGeneration(
            readInt64(properties["generation"], "generation"));
        JsonElement plansElement = properties["plans"];
        requireValueKind(plansElement, JsonValueKind.Array, "plans");
        List<PlanningPlan> plans = new List<PlanningPlan>();
        foreach (JsonElement planElement in plansElement.EnumerateArray())
        {
            plans.Add(readPlan(planElement, schemaVersion));
        }

        if (schemaVersion < CURRENT_SCHEMA_VERSION)
        {
            migrateLegacyPlanNames(plans);
        }

        PlanCatalogBinding catalogBinding;
        PlanId? activePlanIdOrNull;
        if (schemaVersion == CURRENT_SCHEMA_VERSION)
        {
            catalogBinding = readCatalogBinding(
                properties["catalog"],
                "workspace.catalog");
            activePlanIdOrNull = readOptionalPlanIdOrNull(
                properties["activePlanId"],
                "activePlanId");
        }
        else
        {
            if (plans.Count == 0)
            {
                throw new WorkspaceDocumentException(
                    "Legacy planning workspace documents require at least one plan.");
            }

            catalogBinding = plans[0].CatalogBinding;
            activePlanIdOrNull = readPlanId(
                properties["activePlanId"],
                "activePlanId");
        }

        PlanningWorkspace workspace = new PlanningWorkspace(
            catalogBinding,
            activePlanIdOrNull,
            plans);
        return new PlanningWorkspaceDocument(generation, workspace);
    }

    private static int readSchemaVersion(JsonElement element)
    {
        requireValueKind(element, JsonValueKind.Object, "workspace");
        bool hasSchemaVersion = false;
        int schemaVersion = 0;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.NameEquals("schemaVersion"))
            {
                if (hasSchemaVersion)
                {
                    throw new WorkspaceDocumentException(
                        "workspace contains the duplicate property 'schemaVersion'.");
                }

                schemaVersion = readInt32(property.Value, "schemaVersion");
                hasSchemaVersion = true;
            }
        }

        if (hasSchemaVersion == false)
        {
            throw new WorkspaceDocumentException(
                "workspace is missing the required property 'schemaVersion'.");
        }

        return schemaVersion;
    }

    private static PlanningPlan readPlan(JsonElement element, int schemaVersion)
    {
        IReadOnlyList<string> expectedPropertyNames;
        if (schemaVersion == LEGACY_SCHEMA_VERSION)
        {
            expectedPropertyNames = new string[]
            {
                "id",
                "name",
                "catalog",
                "scheduledChoices",
                "unscheduledSelections",
            };
        }
        else if (schemaVersion == PERSONAL_SCHEDULE_SCHEMA_VERSION)
        {
            expectedPropertyNames = new string[]
            {
                "id",
                "name",
                "catalog",
                "scheduledChoices",
                "unscheduledSelections",
                "personalSchedules",
            };
        }
        else if (schemaVersion == COURSE_CHOICE_GROUP_SCHEMA_VERSION)
        {
            expectedPropertyNames = new string[]
            {
                "id",
                "name",
                "catalog",
                "courseChoiceGroups",
                "unscheduledSelections",
                "personalSchedules",
            };
        }
        else
        {
            expectedPropertyNames = new string[]
            {
                "id",
                "name",
                "catalog",
                "courseChoiceGroups",
                "unscheduledSelections",
                "personalSchedules",
                "lastViewedRecommendation",
            };
        }

        Dictionary<string, JsonElement> properties = readExactObject(
            element,
            "plan",
            expectedPropertyNames);
        PlanId planId = readPlanId(properties["id"], "plan.id");
        PlanName planName = new PlanName(readString(properties["name"], "plan.name"));
        PlanCatalogBinding catalogBinding = readCatalogBinding(
            properties["catalog"],
            "plan.catalog");
        IReadOnlyList<CourseChoiceGroup> courseChoiceGroups;
        if (schemaVersion >= COURSE_CHOICE_GROUP_SCHEMA_VERSION)
        {
            courseChoiceGroups = readCourseChoiceGroups(
                properties["courseChoiceGroups"]);
        }
        else
        {
            IReadOnlyList<LegacyScheduledCourseChoiceDocument>
                legacyChoiceDocuments =
                    readLegacyScheduledChoiceDocuments(
                        properties["scheduledChoices"]);
            courseChoiceGroups = migrateLegacyScheduledChoiceDocuments(
                planId,
                legacyChoiceDocuments);
        }
        IReadOnlyList<UnscheduledOfferingSelection> unscheduledSelections =
            readUnscheduledSelections(properties["unscheduledSelections"]);
        IReadOnlyList<PersonalSchedule> personalSchedules;
        if (schemaVersion == LEGACY_SCHEMA_VERSION)
        {
            personalSchedules = Array.Empty<PersonalSchedule>();
        }
        else
        {
            personalSchedules = readPersonalSchedules(properties["personalSchedules"]);
        }

        ScheduleRecommendationBookmark? lastViewedRecommendationOrNull = null;
        if (schemaVersion >= RECOMMENDATION_BOOKMARK_SCHEMA_VERSION)
        {
            lastViewedRecommendationOrNull = readLastViewedRecommendationOrNull(
                properties["lastViewedRecommendation"]);
        }

        return new PlanningPlan(
            planId,
            planName,
            catalogBinding,
            new PlanningPlanContent(
                courseChoiceGroups,
                unscheduledSelections,
                personalSchedules),
            lastViewedRecommendationOrNull);
    }

    private static IReadOnlyList<UnscheduledOfferingSelection>
        readUnscheduledSelections(JsonElement element)
    {
        requireValueKind(element, JsonValueKind.Array, "plan.unscheduledSelections");
        List<UnscheduledOfferingSelection> selections =
            new List<UnscheduledOfferingSelection>();
        foreach (JsonElement selectionElement in element.EnumerateArray())
        {
            Dictionary<string, JsonElement> properties = readExactObject(
                selectionElement,
                "unscheduled selection",
                new string[] { "courseId", "offeringId" });
            CourseId courseId = new CourseId(
                readString(
                    properties["courseId"],
                    "unscheduledSelection.courseId"));
            OfferingId offeringId = new OfferingId(
                readString(
                    properties["offeringId"],
                    "unscheduledSelection.offeringId"));
            selections.Add(new UnscheduledOfferingSelection(courseId, offeringId));
        }

        return selections.AsReadOnly();
    }

    private static PlanId readPlanId(JsonElement element, string context)
    {
        string value = readString(element, context);
        Guid parsedValue;
        if (Guid.TryParseExact(value, "D", out parsedValue) == false)
        {
            throw new WorkspaceDocumentException(
                context + " must be a GUID in D format.");
        }

        return new PlanId(parsedValue);
    }

    private static PlanId? readOptionalPlanIdOrNull(
        JsonElement element,
        string context)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return readPlanId(element, context);
    }

    private static void writeOptionalPlanId(
        Utf8JsonWriter writer,
        string propertyName,
        PlanId? planIdOrNull)
    {
        if (planIdOrNull.HasValue == false)
        {
            writer.WriteNull(propertyName);
            return;
        }

        writer.WriteString(propertyName, planIdOrNull.Value.ToString());
    }

}
