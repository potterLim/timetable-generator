using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarDescriptor
{
    public AppleCalendarId CalendarId { get; }

    public string DisplayName { get; }

    public PlanId? ManagedPlanIdOrNull { get; }

    public EAppleCalendarOwnership Ownership
    {
        get
        {
            return ManagedPlanIdOrNull == null ? EAppleCalendarOwnership.External : EAppleCalendarOwnership.ApplicationManaged;
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

    public AppleCalendarDescriptor(AppleCalendarId id, string displayName, PlanId? managedPlanIdOrNull, EAppleCalendarContentAccess contentAccess)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (displayName == null)
        {
            throw new ArgumentNullException(nameof(displayName));
        }

        string normalizedDisplayName = displayName.Trim();
        if (normalizedDisplayName.Length == 0)
        {
            throw new ArgumentException("Apple calendars require a display name.", nameof(displayName));
        }

        if (managedPlanIdOrNull.HasValue && managedPlanIdOrNull.Value.IsValid == false)
        {
            throw new ArgumentException("Managed Apple calendars require a valid plan ID.", nameof(managedPlanIdOrNull));
        }

        CalendarId = id;
        DisplayName = normalizedDisplayName;
        ManagedPlanIdOrNull = managedPlanIdOrNull;
        ContentAccess = contentAccess;
    }
}
