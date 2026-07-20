using System;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarDescriptor
{
    public AppleCalendarId CalendarId { get; }

    public string DisplayName { get; }

    public bool IsApplicationManaged { get; }

    public bool AllowsContentModification { get; }

    public bool CanReplace
    {
        get
        {
            return IsApplicationManaged && AllowsContentModification;
        }
    }

    public AppleCalendarDescriptor(
        AppleCalendarId id,
        string displayName,
        bool isApplicationManaged,
        bool allowsContentModification)
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
            throw new ArgumentException(
                "Apple calendars require a display name.",
                nameof(displayName));
        }

        CalendarId = id;
        DisplayName = normalizedDisplayName;
        IsApplicationManaged = isApplicationManaged;
        AllowsContentModification = allowsContentModification;
    }
}
