using System;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarExportMutation
{
    public EAppleCalendarExportMutationKind Kind { get; }

    public CalendarExportDocument Document { get; }

    public PlanName DestinationName { get; }

    public AppleCalendarId? ExistingCalendarIdOrNull { get; }

    public string? ExpectedSourceIdentifierOrNull { get; }

    public PlanId CalendarOwnershipPlanId { get; }

    private AppleCalendarExportMutation(
        EAppleCalendarExportMutationKind kind,
        CalendarExportDocument document,
        PlanName destinationName,
        AppleCalendarId? existingCalendarIdOrNull,
        string? expectedSourceIdentifierOrNull,
        PlanId calendarOwnershipPlanId)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (destinationName == null)
        {
            throw new ArgumentNullException(nameof(destinationName));
        }

        if (document.Events.Count == 0)
        {
            throw new ArgumentException("Apple Calendar mutations require at least one calendar event.", nameof(document));
        }

        validateTarget(kind, existingCalendarIdOrNull, expectedSourceIdentifierOrNull, calendarOwnershipPlanId);

        Kind = kind;
        Document = document;
        DestinationName = destinationName;
        ExistingCalendarIdOrNull = existingCalendarIdOrNull;
        if (string.IsNullOrWhiteSpace(expectedSourceIdentifierOrNull))
        {
            ExpectedSourceIdentifierOrNull = null;
        }
        else
        {
            ExpectedSourceIdentifierOrNull = expectedSourceIdentifierOrNull.Trim();
        }

        CalendarOwnershipPlanId = calendarOwnershipPlanId;
    }

    public static AppleCalendarExportMutation CreateNew(CalendarExportDocument document, PlanName destinationName)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        return new AppleCalendarExportMutation(EAppleCalendarExportMutationKind.CreateNew, document, destinationName, null, null, document.PlanId);
    }

    public static AppleCalendarExportMutation ReplaceExisting(
        CalendarExportDocument document,
        PlanName destinationName,
        AppleCalendarId existingCalendarId,
        string expectedSourceIdentifier,
        PlanId calendarOwnershipPlanId)
    {
        if (existingCalendarId == null)
        {
            throw new ArgumentNullException(nameof(existingCalendarId));
        }

        return new AppleCalendarExportMutation(EAppleCalendarExportMutationKind.ReplaceExisting, document, destinationName, existingCalendarId, expectedSourceIdentifier, calendarOwnershipPlanId);
    }

    private static void validateTarget(
        EAppleCalendarExportMutationKind kind,
        AppleCalendarId? existingCalendarIdOrNull,
        string? expectedSourceIdentifierOrNull,
        PlanId calendarOwnershipPlanId)
    {
        if (calendarOwnershipPlanId.IsValid == false)
        {
            throw new ArgumentException("Apple Calendar exports require a valid calendar ownership plan ID.", nameof(calendarOwnershipPlanId));
        }

        switch (kind)
        {
            case EAppleCalendarExportMutationKind.CreateNew:
                if (existingCalendarIdOrNull != null || expectedSourceIdentifierOrNull != null)
                {
                    throw new ArgumentException("New Apple calendars cannot target an existing calendar.", nameof(existingCalendarIdOrNull));
                }

                return;
            case EAppleCalendarExportMutationKind.ReplaceExisting:
                if (existingCalendarIdOrNull == null)
                {
                    throw new ArgumentException("Apple calendar replacement requires an existing calendar ID.", nameof(existingCalendarIdOrNull));
                }

                if (string.IsNullOrWhiteSpace(expectedSourceIdentifierOrNull))
                {
                    throw new ArgumentException("Apple calendar replacement requires an expected source identifier.", nameof(expectedSourceIdentifierOrNull));
                }

                return;
            case EAppleCalendarExportMutationKind.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Apple calendar exports require a supported mutation kind.");
        }
    }
}
