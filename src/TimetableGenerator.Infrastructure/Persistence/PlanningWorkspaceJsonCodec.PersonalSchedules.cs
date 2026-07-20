using System;
using System.Collections.Generic;
using System.Text.Json;

using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed partial class PlanningWorkspaceJsonCodec
{
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
            PersonalScheduleSection? sectionOrNull = readOptionalSectionOrNull(
                properties["section"]);
            PersonalScheduleInstructor? instructorOrNull =
                readOptionalInstructorOrNull(
                    properties["instructor"]);
            PersonalScheduleLocation? locationOrNull =
                readOptionalLocationOrNull(properties["location"]);
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

    private static IReadOnlyList<WeeklyTimeRange> readTimeRanges(
        JsonElement element)
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

    private static PersonalScheduleSection? readOptionalSectionOrNull(
        JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new PersonalScheduleSection(
            readString(element, "personalSchedule.section"));
    }

    private static PersonalScheduleInstructor? readOptionalInstructorOrNull(
        JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new PersonalScheduleInstructor(
            readString(element, "personalSchedule.instructor"));
    }

    private static PersonalScheduleLocation? readOptionalLocationOrNull(
        JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new PersonalScheduleLocation(
            readString(element, "personalSchedule.location"));
    }

    private static string? getSectionValueOrNull(
        PersonalScheduleDetails details)
    {
        if (details.SectionOrNull == null)
        {
            return null;
        }

        return details.SectionOrNull.Value;
    }

    private static string? getInstructorValueOrNull(
        PersonalScheduleDetails details)
    {
        if (details.InstructorOrNull == null)
        {
            return null;
        }

        return details.InstructorOrNull.Value;
    }

    private static string? getLocationValueOrNull(
        PersonalScheduleDetails details)
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
}
