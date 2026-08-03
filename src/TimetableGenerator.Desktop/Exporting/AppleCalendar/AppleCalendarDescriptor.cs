using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarDescriptor
{
    public AppleCalendarId CalendarId { get; }

    public string DisplayName { get; }

    public string SourceIdentifier { get; }

    public PlanId? ManagedPlanIdOrNull { get; }

    public EAppleCalendarOwnership Ownership
    {
        get
        {
            if (ManagedPlanIdOrNull == null)
            {
                return EAppleCalendarOwnership.External;
            }

            return EAppleCalendarOwnership.ApplicationManaged;
        }
    }

    public EAppleCalendarContentAccess ContentAccess { get; }

    public bool CanReplace
    {
        get
        {
            return ManagedPlanIdOrNull != null && ContentAccess == EAppleCalendarContentAccess.Writable;
        }
    }

    public AppleCalendarDescriptor(AppleCalendarId id, string displayName, string sourceIdentifier, PlanId? managedPlanIdOrNull, EAppleCalendarContentAccess contentAccess)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (displayName == null)
        {
            throw new ArgumentNullException(nameof(displayName));
        }

        if (sourceIdentifier == null)
        {
            throw new ArgumentNullException(nameof(sourceIdentifier));
        }

        string normalizedDisplayName = displayName.Trim();
        if (normalizedDisplayName.Length == 0)
        {
            throw new ArgumentException("Apple calendars require a display name.", nameof(displayName));
        }

        string normalizedSourceIdentifier = sourceIdentifier.Trim();
        if (normalizedSourceIdentifier.Length == 0)
        {
            throw new ArgumentException("Apple calendars require a source identifier.", nameof(sourceIdentifier));
        }

        if (managedPlanIdOrNull.HasValue && managedPlanIdOrNull.Value.IsValid == false)
        {
            throw new ArgumentException("Managed Apple calendars require a valid plan ID.", nameof(managedPlanIdOrNull));
        }

        CalendarId = id;
        DisplayName = normalizedDisplayName;
        SourceIdentifier = normalizedSourceIdentifier;
        ManagedPlanIdOrNull = managedPlanIdOrNull;
        ContentAccess = contentAccess;
    }
}
