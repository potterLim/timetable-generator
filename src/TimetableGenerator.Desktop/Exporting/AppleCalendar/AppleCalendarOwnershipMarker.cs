using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarOwnershipMarker
{
    public const string PREFIX =
        "timetable-generator://managed-calendar/v1/";

    public static string CreateForPlan(PlanId planId)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Apple Calendar ownership markers require a valid plan ID.",
                nameof(planId));
        }

        return PREFIX + planId.Value.ToString("D");
    }

    public static bool IsApplicationManaged(string? descriptionOrNull)
    {
        return descriptionOrNull?.StartsWith(
            PREFIX,
            StringComparison.Ordinal) == true;
    }
}
