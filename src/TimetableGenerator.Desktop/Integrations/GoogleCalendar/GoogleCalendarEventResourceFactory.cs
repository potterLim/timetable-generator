using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal static class GoogleCalendarEventResourceFactory
{
    private const string MANAGED_PROPERTY_NAME = "timetableGeneratorManaged";
    private const string PLAN_ID_PROPERTY_NAME = "timetableGeneratorPlanId";
    private const string SOURCE_ID_PROPERTY_NAME = "timetableGeneratorSourceId";

    public static JsonObject Create(
        PlanId planId,
        CalendarTimeZoneId timeZoneId,
        CalendarUtcOffset utcOffset,
        GoogleCalendarExportEvent exportEvent)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar resources require a valid plan ID.",
                nameof(planId));
        }

        if (timeZoneId.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar resources require a valid time-zone ID.",
                nameof(timeZoneId));
        }

        if (utcOffset.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar resources require a valid UTC offset.",
                nameof(utcOffset));
        }

        if (exportEvent == null)
        {
            throw new ArgumentNullException(nameof(exportEvent));
        }

        DateTimeOffset start = resolveLocalDateTime(
            exportEvent.FirstOccurrenceDate,
            exportEvent.StartTime,
            utcOffset);
        DateTimeOffset end = resolveLocalDateTime(
            exportEvent.FirstOccurrenceDate,
            exportEvent.EndTime,
            utcOffset);
        DateTime recurrenceCutoffLocal = exportEvent.LastOccurrenceDate.ToDateTime(
            new TimeOnly(23, 59, 59),
            DateTimeKind.Unspecified);
        DateTimeOffset recurrenceCutoff = new DateTimeOffset(
            recurrenceCutoffLocal,
            utcOffset.Value);
        string recurrenceRule = "RRULE:FREQ=WEEKLY;BYDAY="
            + formatWeekdays(exportEvent.Days)
            + ";UNTIL="
            + recurrenceCutoff
                .ToUniversalTime()
                .ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        GoogleCalendarEventId eventId = GoogleCalendarEventId.Create(
            planId,
            exportEvent.SourceId);

        JsonObject resource = new JsonObject
        {
            ["id"] = eventId.Value,
            ["summary"] = exportEvent.Title,
            ["start"] = createDateTimeResource(start, timeZoneId),
            ["end"] = createDateTimeResource(end, timeZoneId),
            ["recurrence"] = new JsonArray(recurrenceRule),
            ["reminders"] = new JsonObject
            {
                ["useDefault"] = true,
            },
            ["extendedProperties"] = new JsonObject
            {
                ["private"] = new JsonObject
                {
                    [MANAGED_PROPERTY_NAME] = "true",
                    [PLAN_ID_PROPERTY_NAME] = planId.Value.ToString("N"),
                    [SOURCE_ID_PROPERTY_NAME] = exportEvent.SourceId.Value,
                },
            },
        };

        if (exportEvent.Description.Length > 0)
        {
            resource["description"] = exportEvent.Description;
        }

        if (exportEvent.Location.Length > 0)
        {
            resource["location"] = exportEvent.Location;
        }

        return resource;
    }

    public static string CreatePlanPropertyFilter(PlanId planId)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar filters require a valid plan ID.",
                nameof(planId));
        }

        return PLAN_ID_PROPERTY_NAME + "=" + planId.Value.ToString("N");
    }

    public static string CreateManagedPropertyFilter()
    {
        return MANAGED_PROPERTY_NAME + "=true";
    }

    internal static bool isManagedByPlan(
        JsonElement eventResource,
        PlanId planId)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar ownership checks require a valid plan ID.",
                nameof(planId));
        }

        JsonElement extendedProperties;
        JsonElement privateProperties;
        if (eventResource.ValueKind != JsonValueKind.Object
            || eventResource.TryGetProperty(
                "extendedProperties",
                out extendedProperties) == false
            || extendedProperties.ValueKind != JsonValueKind.Object
            || extendedProperties.TryGetProperty(
                "private",
                out privateProperties) == false
            || privateProperties.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        string? managedValueOrNull = getStringOrNull(
            privateProperties,
            MANAGED_PROPERTY_NAME);
        string? planIdValueOrNull = getStringOrNull(
            privateProperties,
            PLAN_ID_PROPERTY_NAME);
        return string.Equals(
            managedValueOrNull,
            "true",
            StringComparison.Ordinal)
            && string.Equals(
                planIdValueOrNull,
                planId.Value.ToString("N"),
                StringComparison.Ordinal);
    }

    private static JsonObject createDateTimeResource(
        DateTimeOffset dateTime,
        CalendarTimeZoneId timeZoneId)
    {
        return new JsonObject
        {
            ["dateTime"] = dateTime.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ["timeZone"] = timeZoneId.Value,
        };
    }

    private static DateTimeOffset resolveLocalDateTime(
        DateOnly date,
        TimeOnly time,
        CalendarUtcOffset utcOffset)
    {
        DateTime localDateTime = date.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(localDateTime, utcOffset.Value);
    }

    private static string formatWeekdays(IReadOnlyList<EDay> days)
    {
        StringBuilder builder = new StringBuilder();
        foreach (EDay day in days)
        {
            if (builder.Length > 0)
            {
                builder.Append(',');
            }

            builder.Append(formatWeekday(day));
        }

        return builder.ToString();
    }

    private static string formatWeekday(EDay day)
    {
        return day switch
        {
            EDay.Monday => "MO",
            EDay.Tuesday => "TU",
            EDay.Wednesday => "WE",
            EDay.Thursday => "TH",
            EDay.Friday => "FR",
            EDay.Saturday => "SA",
            EDay.Sunday => "SU",
            _ => throw new ArgumentOutOfRangeException(nameof(day)),
        };
    }

    private static string? getStringOrNull(
        JsonElement element,
        string propertyName)
    {
        JsonElement property;
        if (element.TryGetProperty(propertyName, out property) == false
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }
}
