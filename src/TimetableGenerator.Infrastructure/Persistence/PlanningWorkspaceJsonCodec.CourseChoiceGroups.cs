using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed partial class PlanningWorkspaceJsonCodec
{
    private const int GUID_BYTE_COUNT = 16;

    private const string LEGACY_COURSE_CHOICE_GROUP_ID_NAMESPACE =
        "timetable-generator:legacy-course-choice-group";

    private static void writeCourseChoiceGroup(
        Utf8JsonWriter writer,
        CourseChoiceGroup courseChoiceGroup)
    {
        writer.WriteStartObject();
        writer.WriteString("id", courseChoiceGroup.Id.ToString());
        writer.WriteString(
            "cardinality",
            getCardinalityJsonValue(courseChoiceGroup.Cardinality));
        writer.WriteStartArray("courseCandidates");
        foreach (CourseCandidate courseCandidate
            in courseChoiceGroup.CourseCandidates)
        {
            writer.WriteStartObject();
            writer.WriteString("courseId", courseCandidate.CourseId.Value);
            writer.WriteStartArray("offeringCandidates");
            foreach (OfferingCandidate offeringCandidate
                in courseCandidate.OfferingCandidates)
            {
                writer.WriteStartObject();
                writer.WriteString(
                    "offeringId",
                    offeringCandidate.OfferingId.Value);
                writer.WriteString(
                    "preference",
                    getOfferingPreferenceJsonValue(offeringCandidate.Preference));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static IReadOnlyList<CourseChoiceGroup> readCourseChoiceGroups(
        JsonElement element)
    {
        requireValueKind(element, JsonValueKind.Array, "plan.courseChoiceGroups");
        List<CourseChoiceGroup> courseChoiceGroups = new List<CourseChoiceGroup>();
        foreach (JsonElement groupElement in element.EnumerateArray())
        {
            Dictionary<string, JsonElement> properties = readExactObject(
                groupElement,
                "course choice group",
                new string[] { "id", "cardinality", "courseCandidates" });
            CourseChoiceGroupId groupId = readCourseChoiceGroupId(
                properties["id"],
                "courseChoiceGroup.id");
            ECourseChoiceCardinality cardinality = readCourseChoiceCardinality(
                properties["cardinality"]);
            IReadOnlyList<CourseCandidate> courseCandidates = readCourseCandidates(
                properties["courseCandidates"]);
            courseChoiceGroups.Add(new CourseChoiceGroup(
                groupId,
                cardinality,
                courseCandidates));
        }

        return courseChoiceGroups.AsReadOnly();
    }

    private static IReadOnlyList<CourseCandidate> readCourseCandidates(
        JsonElement element)
    {
        requireValueKind(
            element,
            JsonValueKind.Array,
            "courseChoiceGroup.courseCandidates");
        List<CourseCandidate> courseCandidates = new List<CourseCandidate>();
        foreach (JsonElement courseElement in element.EnumerateArray())
        {
            Dictionary<string, JsonElement> properties = readExactObject(
                courseElement,
                "course candidate",
                new string[] { "courseId", "offeringCandidates" });
            CourseId courseId = new CourseId(
                readString(properties["courseId"], "courseCandidate.courseId"));
            IReadOnlyList<OfferingCandidate> offeringCandidates =
                readOfferingCandidates(properties["offeringCandidates"]);
            courseCandidates.Add(new CourseCandidate(courseId, offeringCandidates));
        }

        return courseCandidates.AsReadOnly();
    }

    private static IReadOnlyList<OfferingCandidate> readOfferingCandidates(
        JsonElement element)
    {
        requireValueKind(
            element,
            JsonValueKind.Array,
            "courseCandidate.offeringCandidates");
        List<OfferingCandidate> offeringCandidates =
            new List<OfferingCandidate>();
        foreach (JsonElement offeringElement in element.EnumerateArray())
        {
            Dictionary<string, JsonElement> properties = readExactObject(
                offeringElement,
                "offering candidate",
                new string[] { "offeringId", "preference" });
            OfferingId offeringId = new OfferingId(
                readString(
                    properties["offeringId"],
                    "offeringCandidate.offeringId"));
            EOfferingPreference preference = readOfferingPreference(
                properties["preference"]);
            offeringCandidates.Add(new OfferingCandidate(offeringId, preference));
        }

        return offeringCandidates.AsReadOnly();
    }

    private static IReadOnlyList<CourseChoiceGroup> migrateScheduledChoices(
        PlanId planId,
        IReadOnlyList<ScheduledCourseChoice> scheduledChoices)
    {
        List<CourseChoiceGroup> courseChoiceGroups =
            new List<CourseChoiceGroup>(scheduledChoices.Count);
        for (int choiceIndex = 0;
            choiceIndex < scheduledChoices.Count;
            ++choiceIndex)
        {
            ScheduledCourseChoice scheduledChoice = scheduledChoices[choiceIndex];
            CourseChoiceGroupId groupId = createLegacyCourseChoiceGroupId(
                planId,
                scheduledChoice.CourseId,
                choiceIndex);
            courseChoiceGroups.Add(
                CourseChoiceGroup.CreateFromScheduledCourseChoice(
                    groupId,
                    scheduledChoice));
        }

        return courseChoiceGroups.AsReadOnly();
    }

    private static CourseChoiceGroupId createLegacyCourseChoiceGroupId(
        PlanId planId,
        CourseId courseId,
        int choiceIndex)
    {
        string identity = string.Concat(
            LEGACY_COURSE_CHOICE_GROUP_ID_NAMESPACE,
            "|",
            planId.ToString(),
            "|",
            choiceIndex.ToString(CultureInfo.InvariantCulture),
            "|",
            courseId.Value);
        byte[] identityBytes = Encoding.UTF8.GetBytes(identity);
        byte[] digest = SHA256.HashData(identityBytes);
        byte[] guidBytes = new byte[GUID_BYTE_COUNT];
        Array.Copy(digest, guidBytes, GUID_BYTE_COUNT);
        return new CourseChoiceGroupId(new Guid(guidBytes));
    }

    private static CourseChoiceGroupId readCourseChoiceGroupId(
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

        return new CourseChoiceGroupId(parsedValue);
    }

    private static ECourseChoiceCardinality readCourseChoiceCardinality(
        JsonElement element)
    {
        string value = readString(element, "courseChoiceGroup.cardinality");
        switch (value)
        {
            case "exactlyOne":
                return ECourseChoiceCardinality.ExactlyOne;
            default:
                throw new WorkspaceDocumentException(
                    "courseChoiceGroup.cardinality is not supported.");
        }
    }

    private static EOfferingPreference readOfferingPreference(JsonElement element)
    {
        string value = readString(element, "offeringCandidate.preference");
        switch (value)
        {
            case "preferred":
                return EOfferingPreference.Preferred;
            case "acceptable":
                return EOfferingPreference.Acceptable;
            case "excluded":
                return EOfferingPreference.Excluded;
            default:
                throw new WorkspaceDocumentException(
                    "offeringCandidate.preference is not supported.");
        }
    }

    private static string getCardinalityJsonValue(
        ECourseChoiceCardinality cardinality)
    {
        switch (cardinality)
        {
            case ECourseChoiceCardinality.ExactlyOne:
                return "exactlyOne";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(cardinality),
                    cardinality,
                    "Unknown course choice cardinality.");
        }
    }

    private static string getOfferingPreferenceJsonValue(
        EOfferingPreference preference)
    {
        switch (preference)
        {
            case EOfferingPreference.Preferred:
                return "preferred";
            case EOfferingPreference.Acceptable:
                return "acceptable";
            case EOfferingPreference.Excluded:
                return "excluded";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(preference),
                    preference,
                    "Unknown offering preference.");
        }
    }
}
