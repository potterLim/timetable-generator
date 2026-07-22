using System;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarDescriptor
{
    public AppleCalendarId CalendarId { get; }

    public string DisplayName { get; }

    public EAppleCalendarOwnership Ownership { get; }

    public EAppleCalendarContentAccess ContentAccess { get; }

    public bool CanReplace
    {
        get
        {
            return Ownership == EAppleCalendarOwnership.ApplicationManaged
                && ContentAccess == EAppleCalendarContentAccess.Writable;
        }
    }

    public AppleCalendarDescriptor(
        AppleCalendarId id,
        string displayName,
        EAppleCalendarOwnership ownership,
        EAppleCalendarContentAccess contentAccess)
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

        CalendarId = id;
        DisplayName = normalizedDisplayName;
        Ownership = ownership;
        ContentAccess = contentAccess;
    }
}
