using System;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarDescriptor
{
    public GoogleCalendarId CalendarId { get; }

    public string DisplayName { get; }

    public bool IsPrimary { get; }

    public PlanId? ManagedPlanIdOrNull { get; }

    public bool IsApplicationManaged
    {
        get
        {
            return ManagedPlanIdOrNull != null;
        }
    }

    public EGoogleCalendarAccessRole AccessRole { get; }

    public bool CanWrite
    {
        get
        {
            return AccessRole == EGoogleCalendarAccessRole.Writer
                || AccessRole
                    == EGoogleCalendarAccessRole.WriterWithoutPrivateAccess
                || AccessRole == EGoogleCalendarAccessRole.Owner;
        }
    }

    public bool CanReplace
    {
        get
        {
            return IsPrimary == false
                && IsApplicationManaged
                && CanWrite;
        }
    }

    public GoogleCalendarDescriptor(
        GoogleCalendarId calendarId,
        string displayName,
        bool isPrimary,
        PlanId? managedPlanIdOrNull,
        EGoogleCalendarAccessRole accessRole)
    {
        if (calendarId == null)
        {
            throw new ArgumentNullException(nameof(calendarId));
        }

        if (displayName == null)
        {
            throw new ArgumentNullException(nameof(displayName));
        }

        string normalizedDisplayName = displayName.Trim();
        if (normalizedDisplayName.Length == 0)
        {
            throw new ArgumentException(
                "Google calendars require a display name.",
                nameof(displayName));
        }

        if (managedPlanIdOrNull.HasValue
            && managedPlanIdOrNull.Value.IsValid == false)
        {
            throw new ArgumentException(
                "Managed Google calendars require a valid plan ID.",
                nameof(managedPlanIdOrNull));
        }

        validateAccessRole(accessRole);

        CalendarId = calendarId;
        DisplayName = normalizedDisplayName;
        IsPrimary = isPrimary;
        ManagedPlanIdOrNull = managedPlanIdOrNull;
        AccessRole = accessRole;
    }

    private static void validateAccessRole(
        EGoogleCalendarAccessRole accessRole)
    {
        switch (accessRole)
        {
            case EGoogleCalendarAccessRole.None:
            case EGoogleCalendarAccessRole.FreeBusyReader:
            case EGoogleCalendarAccessRole.Reader:
            case EGoogleCalendarAccessRole.Writer:
            case EGoogleCalendarAccessRole.WriterWithoutPrivateAccess:
            case EGoogleCalendarAccessRole.Owner:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(accessRole),
                    accessRole,
                    "Unknown Google Calendar access role.");
        }
    }
}
