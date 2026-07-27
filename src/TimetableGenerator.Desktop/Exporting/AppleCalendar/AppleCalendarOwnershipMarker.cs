using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarOwnershipMarker
{
    public const string PREFIX = "timetable-generator://managed-calendar/v1/";

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
        return TryParsePlanIdOrNull(descriptionOrNull) != null;
    }

    public static PlanId? TryParsePlanIdOrNull(string? descriptionOrNull)
    {
        if (descriptionOrNull?.StartsWith(
                PREFIX,
                StringComparison.Ordinal) != true)
        {
            return null;
        }

        Guid planIdValue;
        return Guid.TryParseExact(
                descriptionOrNull[PREFIX.Length..],
                "D",
                out planIdValue)
            && planIdValue != Guid.Empty
            ? new PlanId(planIdValue)
            : null;
    }
}
