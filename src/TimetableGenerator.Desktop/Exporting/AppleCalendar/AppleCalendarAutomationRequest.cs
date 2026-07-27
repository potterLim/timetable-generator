using System;
using System.Collections.Generic;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarAutomationRequest
{
    private readonly IReadOnlyList<AppleCalendarAutomationEvent> mEvents;

    public string OwnershipMarkerPrefix { get; }

    public string OwnershipDescription { get; }

    public string CalendarDescription { get; }

    public string LegacyEventOwnershipMarkerPrefix { get; }

    public string EventOwnershipMarkerPrefix { get; }

    public string PlanId { get; }

    public string MutationKind { get; }

    public string DestinationName { get; }

    public string NormalizedDestinationName { get; }

    public string ExistingCalendarId { get; }

    public IReadOnlyList<AppleCalendarAutomationEvent> Events
    {
        get
        {
            return mEvents;
        }
    }

    private AppleCalendarAutomationRequest(
        string ownershipDescription,
        string calendarDescription,
        string planId,
        string mutationKind,
        string destinationName,
        string normalizedDestinationName,
        string existingCalendarId,
        IReadOnlyList<AppleCalendarAutomationEvent> events)
    {
        OwnershipMarkerPrefix = AppleCalendarOwnershipMarker.PREFIX;
        LegacyEventOwnershipMarkerPrefix = AppleCalendarEventOwnershipMarker.LEGACY_PREFIX;
        EventOwnershipMarkerPrefix = AppleCalendarEventOwnershipMarker.PREFIX;
        OwnershipDescription = ownershipDescription;
        CalendarDescription = calendarDescription;
        PlanId = planId;
        MutationKind = mutationKind;
        DestinationName = destinationName;
        NormalizedDestinationName = normalizedDestinationName;
        ExistingCalendarId = existingCalendarId;
        mEvents = events;
    }

    public static AppleCalendarAutomationRequest CreateListRequest(PlanName requestedDestinationName)
    {
        if (requestedDestinationName == null)
        {
            throw new ArgumentNullException(nameof(requestedDestinationName));
        }

        return new AppleCalendarAutomationRequest(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            requestedDestinationName.Value,
            normalizeName(requestedDestinationName.Value),
            string.Empty,
            Array.Empty<AppleCalendarAutomationEvent>());
    }

    public static AppleCalendarAutomationRequest CreateMutationRequest(
        AppleCalendarExportMutation mutation)
    {
        if (mutation == null)
        {
            throw new ArgumentNullException(nameof(mutation));
        }

        string mutationKind;
        switch (mutation.Kind)
        {
            case EAppleCalendarExportMutationKind.CreateNew:
                mutationKind = "create";
                break;
            case EAppleCalendarExportMutationKind.ReplaceExisting:
                mutationKind = "replace";
                break;
            case EAppleCalendarExportMutationKind.None:
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation.Kind,
                    "Apple Calendar automation requires a supported mutation.");
        }

        IReadOnlyList<AppleCalendarAutomationEvent> events = AppleCalendarEventOccurrenceProjector.Project(mutation.Document, mutation.CalendarOwnershipPlanId);
        string existingCalendarId = mutation.ExistingCalendarIdOrNull == null ? string.Empty : mutation.ExistingCalendarIdOrNull.Value;
        return new AppleCalendarAutomationRequest(
            AppleCalendarOwnershipMarker.CreateForPlan(mutation.CalendarOwnershipPlanId),
            AppleCalendarDescription.Create(mutation.Document.InstitutionName, mutation.Document.AcademicCalendar.Term).Value,
            mutation.CalendarOwnershipPlanId.Value.ToString("D"),
            mutationKind,
            mutation.DestinationName.Value,
            normalizeName(mutation.DestinationName.Value),
            existingCalendarId,
            events);
    }

    private static string normalizeName(string value)
    {
        return value.Trim().Normalize().ToUpperInvariant();
    }
}
