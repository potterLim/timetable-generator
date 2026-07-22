using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class CalendarNameConflict
{
    public ECalendarExportProvider Provider { get; }

    public PlanName RequestedName { get; }

    public PlanName NextAvailableName { get; }

    public ECalendarReplacementAvailability ReplacementAvailability { get; }

    public bool CanReplace
    {
        get
        {
            return ReplacementAvailability == ECalendarReplacementAvailability.Available;
        }
    }

    public CalendarNameConflict(
        ECalendarExportProvider provider,
        PlanName requestedName,
        PlanName nextAvailableName,
        ECalendarReplacementAvailability replacementAvailability)
    {
        validateProvider(provider);
        if (requestedName == null)
        {
            throw new ArgumentNullException(nameof(requestedName));
        }

        if (nextAvailableName == null)
        {
            throw new ArgumentNullException(nameof(nextAvailableName));
        }

        validateReplacementAvailability(replacementAvailability);
        if (CalendarNameConflictPolicy.IsSameName(requestedName, nextAvailableName))
        {
            throw new ArgumentException(
                "The next available calendar name must differ from the requested name.",
                nameof(nextAvailableName));
        }

        Provider = provider;
        RequestedName = requestedName;
        NextAvailableName = nextAvailableName;
        ReplacementAvailability = replacementAvailability;
    }

    private static void validateProvider(ECalendarExportProvider provider)
    {
        switch (provider)
        {
            case ECalendarExportProvider.Google:
            case ECalendarExportProvider.Apple:
                return;
            case ECalendarExportProvider.None:
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(provider),
                    provider,
                    "Calendar name conflicts require a supported export provider.");
        }
    }

    private static void validateReplacementAvailability(
        ECalendarReplacementAvailability replacementAvailability)
    {
        switch (replacementAvailability)
        {
            case ECalendarReplacementAvailability.Unavailable:
            case ECalendarReplacementAvailability.Available:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(replacementAvailability),
                    replacementAvailability,
                    "Calendar name conflicts require a valid replacement availability.");
        }
    }
}
