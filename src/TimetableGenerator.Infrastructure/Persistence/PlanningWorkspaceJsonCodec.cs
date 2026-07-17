using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed partial class PlanningWorkspaceJsonCodec
{
    private const int CURRENT_SCHEMA_VERSION = 3;
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
        writer.WriteString("activePlanId", workspace.ActivePlanId.ToString());
        writer.WriteStartArray("plans");
        foreach (PlanningPlan plan in workspace.Plans)
        {
            writePlan(writer, plan);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void writePersonalSchedule(
        Utf8JsonWriter writer,
        PersonalSchedule personalSchedule)
    {
        writer.WriteStartObject();
        writer.WriteString("id", personalSchedule.Id.ToString());
        writer.WriteString("title", personalSchedule.Title.Value);
        writer.WriteStartArray("timeRanges");
        foreach (WeeklyTimeRange timeRange in personalSchedule.TimeRanges)
        {
            writer.WriteStartObject();
            writer.WriteString("day", getDayJsonValue(timeRange.Day));
            writer.WriteString("start", timeRange.TimeRange.Start.ToString());
            writer.WriteString("end", timeRange.TimeRange.End.ToString());
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writeOptionalString(
            writer,
            "section",
            getSectionValueOrNull(personalSchedule.Details));
        writeOptionalString(
            writer,
            "instructor",
            getInstructorValueOrNull(personalSchedule.Details));
        writeOptionalString(
            writer,
            "location",
            getLocationValueOrNull(personalSchedule.Details));
        writer.WriteEndObject();
    }

    private static void writePlan(Utf8JsonWriter writer, PlanningPlan plan)
    {
        writer.WriteStartObject();
        writer.WriteString("id", plan.Id.ToString());
        writer.WriteString("name", plan.Name.Value);
        writer.WriteStartObject("catalog");
        writer.WriteString("catalogId", plan.CatalogBinding.CatalogId.Value);
        writer.WriteString("institutionId", plan.CatalogBinding.InstitutionId.Value);
        writer.WriteString("term", plan.CatalogBinding.Term.Id);
        writer.WriteNumber("revision", plan.CatalogBinding.Revision.Value);
        writer.WriteString(
            "artifactSha256",
            plan.CatalogBinding.ArtifactSha256.HexValue);
        writer.WriteEndObject();
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
        writer.WriteEndObject();
    }

    private static PlanningWorkspaceDocument readWorkspace(JsonElement element)
    {
        int schemaVersion = readSchemaVersion(element);
        switch (schemaVersion)
        {
            case LEGACY_SCHEMA_VERSION:
            case PERSONAL_SCHEDULE_SCHEMA_VERSION:
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
        Dictionary<string, JsonElement> properties = readExactObject(
            element,
            "workspace",
            new string[] { "schemaVersion", "generation", "activePlanId", "plans" });
        WorkspaceGeneration generation = new WorkspaceGeneration(
            readInt64(properties["generation"], "generation"));
        PlanId activePlanId = readPlanId(properties["activePlanId"], "activePlanId");
        JsonElement plansElement = properties["plans"];
        requireValueKind(plansElement, JsonValueKind.Array, "plans");
        List<PlanningPlan> plans = new List<PlanningPlan>();
        foreach (JsonElement planElement in plansElement.EnumerateArray())
        {
            plans.Add(readPlan(planElement, schemaVersion));
        }

        PlanningWorkspace workspace = new PlanningWorkspace(activePlanId, plans);
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
            };
        }

        Dictionary<string, JsonElement> properties = readExactObject(
            element,
            "plan",
            expectedPropertyNames);
        PlanId planId = readPlanId(properties["id"], "plan.id");
        PlanName planName = new PlanName(readString(properties["name"], "plan.name"));
        PlanCatalogBinding catalogBinding = readCatalogBinding(properties["catalog"]);
        IReadOnlyList<CourseChoiceGroup> courseChoiceGroups;
        if (schemaVersion == CURRENT_SCHEMA_VERSION)
        {
            courseChoiceGroups = readCourseChoiceGroups(
                properties["courseChoiceGroups"]);
        }
        else
        {
            IReadOnlyList<ScheduledCourseChoice> scheduledChoices =
                readScheduledChoices(properties["scheduledChoices"]);
            courseChoiceGroups = migrateScheduledChoices(
                planId,
                scheduledChoices);
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

        return new PlanningPlan(
            planId,
            planName,
            catalogBinding,
            new PlanningPlanContent(
                courseChoiceGroups,
                unscheduledSelections,
                personalSchedules));
    }

    private static IReadOnlyList<PersonalSchedule> readPersonalSchedules(
        JsonElement element)
    {
        requireValueKind(element, JsonValueKind.Array, "plan.personalSchedules");
        List<PersonalSchedule> personalSchedules = new List<PersonalSchedule>();
        foreach (JsonElement scheduleElement in element.EnumerateArray())
        {
            Dictionary<string, JsonElement> properties = readExactObject(
                scheduleElement,
                "personal schedule",
                new string[]
                {
                    "id",
                    "title",
                    "timeRanges",
                    "section",
                    "instructor",
                    "location",
                });
            PersonalScheduleId scheduleId = readPersonalScheduleId(
                properties["id"],
                "personalSchedule.id");
            PersonalScheduleTitle title = new PersonalScheduleTitle(
                readString(properties["title"], "personalSchedule.title"));
            IReadOnlyList<WeeklyTimeRange> timeRanges = readTimeRanges(
                properties["timeRanges"]);
            PersonalScheduleSection? sectionOrNull = readOptionalSection(
                properties["section"]);
            PersonalScheduleInstructor? instructorOrNull = readOptionalInstructor(
                properties["instructor"]);
            PersonalScheduleLocation? locationOrNull = readOptionalLocation(
                properties["location"]);
            PersonalScheduleDetails details = new PersonalScheduleDetails(
                sectionOrNull,
                instructorOrNull,
                locationOrNull);
            personalSchedules.Add(new PersonalSchedule(
                scheduleId,
                title,
                timeRanges,
                details));
        }

        return personalSchedules.AsReadOnly();
    }

    private static IReadOnlyList<WeeklyTimeRange> readTimeRanges(JsonElement element)
    {
        requireValueKind(element, JsonValueKind.Array, "personalSchedule.timeRanges");
        List<WeeklyTimeRange> timeRanges = new List<WeeklyTimeRange>();
        foreach (JsonElement timeRangeElement in element.EnumerateArray())
        {
            Dictionary<string, JsonElement> properties = readExactObject(
                timeRangeElement,
                "personal schedule time range",
                new string[] { "day", "start", "end" });
            EDay day = readDay(properties["day"]);
            ScheduleTime start = readScheduleTime(
                properties["start"],
                "personalSchedule.timeRanges[].start");
            ScheduleTime end = readScheduleTime(
                properties["end"],
                "personalSchedule.timeRanges[].end");
            timeRanges.Add(new WeeklyTimeRange(
                day,
                new DailyTimeRange(start, end)));
        }

        return timeRanges.AsReadOnly();
    }

    private static PlanCatalogBinding readCatalogBinding(JsonElement element)
    {
        Dictionary<string, JsonElement> properties = readExactObject(
            element,
            "plan.catalog",
            new string[]
            {
                "catalogId",
                "institutionId",
                "term",
                "revision",
                "artifactSha256",
            });
        CatalogId catalogId = new CatalogId(
            readString(properties["catalogId"], "plan.catalog.catalogId"));
        InstitutionId institutionId = new InstitutionId(
            readString(properties["institutionId"], "plan.catalog.institutionId"));
        AcademicTerm term = AcademicTerm.Parse(
            readString(properties["term"], "plan.catalog.term"));
        CatalogRevision revision = new CatalogRevision(
            readInt32(properties["revision"], "plan.catalog.revision"));
        CatalogArtifactSha256 artifactSha256 =
            new CatalogArtifactSha256(
                readString(
                    properties["artifactSha256"],
                    "plan.catalog.artifactSha256"));
        return new PlanCatalogBinding(
            catalogId,
            institutionId,
            term,
            revision,
            artifactSha256);
    }

    private static IReadOnlyList<ScheduledCourseChoice> readScheduledChoices(
        JsonElement element)
    {
        requireValueKind(element, JsonValueKind.Array, "plan.scheduledChoices");
        List<ScheduledCourseChoice> choices = new List<ScheduledCourseChoice>();
        foreach (JsonElement choiceElement in element.EnumerateArray())
        {
            Dictionary<string, JsonElement> properties = readExactObject(
                choiceElement,
                "scheduled choice",
                new string[] { "courseId", "offeringIds" });
            CourseId courseId = new CourseId(
                readString(properties["courseId"], "scheduledChoice.courseId"));
            JsonElement offeringIdsElement = properties["offeringIds"];
            requireValueKind(
                offeringIdsElement,
                JsonValueKind.Array,
                "scheduledChoice.offeringIds");
            List<OfferingId> offeringIds = new List<OfferingId>();
            foreach (JsonElement offeringIdElement in offeringIdsElement.EnumerateArray())
            {
                offeringIds.Add(
                    new OfferingId(
                        readString(
                            offeringIdElement,
                            "scheduledChoice.offeringIds[]")));
            }

            choices.Add(new ScheduledCourseChoice(courseId, offeringIds));
        }

        return choices.AsReadOnly();
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

    private static PersonalScheduleId readPersonalScheduleId(
        JsonElement element,
        string context)
    {
        string value = readString(element, context);
        Guid parsedValue;
        if (Guid.TryParseExact(value, "D", out parsedValue) == false)
        {
            throw new WorkspaceDocumentException(
                context + " must be a GUID in D format.");
        }

        return new PersonalScheduleId(parsedValue);
    }

    private static ScheduleTime readScheduleTime(
        JsonElement element,
        string context)
    {
        string value = readString(element, context);
        if (value.Length != 5 || value[2] != ':')
        {
            throw new WorkspaceDocumentException(
                context + " must use the HH:mm format.");
        }

        int hour;
        int minute;
        bool hasHour = int.TryParse(
            value.Substring(0, 2),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out hour);
        bool hasMinute = int.TryParse(
            value.Substring(3, 2),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out minute);
        if (hasHour == false || hasMinute == false)
        {
            throw new WorkspaceDocumentException(
                context + " must use the HH:mm format.");
        }

        return new ScheduleTime(hour, minute);
    }

    private static EDay readDay(JsonElement element)
    {
        string value = readString(element, "personalSchedule.timeRanges[].day");
        switch (value)
        {
            case "monday":
                return EDay.Monday;
            case "tuesday":
                return EDay.Tuesday;
            case "wednesday":
                return EDay.Wednesday;
            case "thursday":
                return EDay.Thursday;
            case "friday":
                return EDay.Friday;
            case "saturday":
                return EDay.Saturday;
            case "sunday":
                return EDay.Sunday;
            default:
                throw new WorkspaceDocumentException(
                    "personalSchedule.timeRanges[].day is not a supported day.");
        }
    }

    private static string getDayJsonValue(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return "monday";
            case EDay.Tuesday:
                return "tuesday";
            case EDay.Wednesday:
                return "wednesday";
            case EDay.Thursday:
                return "thursday";
            case EDay.Friday:
                return "friday";
            case EDay.Saturday:
                return "saturday";
            case EDay.Sunday:
                return "sunday";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Unknown schedule day.");
        }
    }

    private static PersonalScheduleSection? readOptionalSection(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new PersonalScheduleSection(
            readString(element, "personalSchedule.section"));
    }

    private static PersonalScheduleInstructor? readOptionalInstructor(
        JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new PersonalScheduleInstructor(
            readString(element, "personalSchedule.instructor"));
    }

    private static PersonalScheduleLocation? readOptionalLocation(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new PersonalScheduleLocation(
            readString(element, "personalSchedule.location"));
    }

    private static string? getSectionValueOrNull(PersonalScheduleDetails details)
    {
        if (details.SectionOrNull == null)
        {
            return null;
        }

        return details.SectionOrNull.Value;
    }

    private static string? getInstructorValueOrNull(PersonalScheduleDetails details)
    {
        if (details.InstructorOrNull == null)
        {
            return null;
        }

        return details.InstructorOrNull.Value;
    }

    private static string? getLocationValueOrNull(PersonalScheduleDetails details)
    {
        if (details.LocationOrNull == null)
        {
            return null;
        }

        return details.LocationOrNull.Value;
    }

    private static void writeOptionalString(
        Utf8JsonWriter writer,
        string propertyName,
        string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            writer.WriteNull(propertyName);
        }
        else
        {
            writer.WriteString(propertyName, valueOrNull);
        }
    }

    private static Dictionary<string, JsonElement> readExactObject(
        JsonElement element,
        string context,
        IReadOnlyList<string> expectedPropertyNames)
    {
        requireValueKind(element, JsonValueKind.Object, context);
        HashSet<string> expectedNames = new HashSet<string>(
            expectedPropertyNames,
            StringComparer.Ordinal);
        Dictionary<string, JsonElement> properties = new Dictionary<string, JsonElement>(
            StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (expectedNames.Contains(property.Name) == false)
            {
                throw new WorkspaceDocumentException(
                    context + " contains the unknown property '" + property.Name + "'.");
            }

            if (properties.TryAdd(property.Name, property.Value) == false)
            {
                throw new WorkspaceDocumentException(
                    context + " contains the duplicate property '" + property.Name + "'.");
            }
        }

        foreach (string expectedPropertyName in expectedPropertyNames)
        {
            if (properties.ContainsKey(expectedPropertyName) == false)
            {
                throw new WorkspaceDocumentException(
                    context
                    + " is missing the required property '"
                    + expectedPropertyName
                    + "'.");
            }
        }

        return properties;
    }

    private static string readString(JsonElement element, string context)
    {
        requireValueKind(element, JsonValueKind.String, context);
        string? value = element.GetString();
        if (value == null)
        {
            throw new WorkspaceDocumentException(context + " cannot be null.");
        }

        return value;
    }

    private static int readInt32(JsonElement element, string context)
    {
        requireValueKind(element, JsonValueKind.Number, context);
        int value;
        if (element.TryGetInt32(out value) == false)
        {
            throw new WorkspaceDocumentException(context + " must be a 32-bit integer.");
        }

        return value;
    }

    private static long readInt64(JsonElement element, string context)
    {
        requireValueKind(element, JsonValueKind.Number, context);
        long value;
        if (element.TryGetInt64(out value) == false)
        {
            throw new WorkspaceDocumentException(context + " must be a 64-bit integer.");
        }

        return value;
    }

    private static void requireValueKind(
        JsonElement element,
        JsonValueKind expectedKind,
        string context)
    {
        if (element.ValueKind != expectedKind)
        {
            throw new WorkspaceDocumentException(
                context + " must be a JSON " + expectedKind + " value.");
        }
    }
}
