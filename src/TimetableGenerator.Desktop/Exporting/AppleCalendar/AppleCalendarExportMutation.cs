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

    private AppleCalendarExportMutation(
        EAppleCalendarExportMutationKind kind,
        CalendarExportDocument document,
        PlanName destinationName,
        AppleCalendarId? existingCalendarIdOrNull)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (destinationName == null)
        {
            throw new ArgumentNullException(nameof(destinationName));
        }

        validateTarget(kind, existingCalendarIdOrNull);

        Kind = kind;
        Document = document;
        DestinationName = destinationName;
        ExistingCalendarIdOrNull = existingCalendarIdOrNull;
    }

    public static AppleCalendarExportMutation CreateNew(
        CalendarExportDocument document,
        PlanName destinationName)
    {
        return new AppleCalendarExportMutation(
            EAppleCalendarExportMutationKind.CreateNew,
            document,
            destinationName,
            null);
    }

    public static AppleCalendarExportMutation ReplaceExisting(
        CalendarExportDocument document,
        PlanName destinationName,
        AppleCalendarId existingCalendarId)
    {
        if (existingCalendarId == null)
        {
            throw new ArgumentNullException(nameof(existingCalendarId));
        }

        return new AppleCalendarExportMutation(
            EAppleCalendarExportMutationKind.ReplaceExisting,
            document,
            destinationName,
            existingCalendarId);
    }

    private static void validateTarget(
        EAppleCalendarExportMutationKind kind,
        AppleCalendarId? existingCalendarIdOrNull)
    {
        switch (kind)
        {
            case EAppleCalendarExportMutationKind.CreateNew:
                if (existingCalendarIdOrNull != null)
                {
                    throw new ArgumentException(
                        "New Apple calendars cannot target an existing calendar ID.",
                        nameof(existingCalendarIdOrNull));
                }

                return;
            case EAppleCalendarExportMutationKind.ReplaceExisting:
                if (existingCalendarIdOrNull == null)
                {
                    throw new ArgumentException(
                        "Apple calendar replacement requires an existing calendar ID.",
                        nameof(existingCalendarIdOrNull));
                }

                return;
            case EAppleCalendarExportMutationKind.None:
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Apple calendar exports require a supported mutation kind.");
        }
    }
}
