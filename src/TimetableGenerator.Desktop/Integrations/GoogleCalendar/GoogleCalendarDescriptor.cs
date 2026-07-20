using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class GoogleCalendarDescriptor
{
    public GoogleCalendarId CalendarId { get; }

    public string DisplayName { get; }

    public bool IsPrimary { get; }

    public bool IsApplicationManaged { get; }

    public bool CanReplace
    {
        get
        {
            return IsPrimary == false && IsApplicationManaged;
        }
    }

    public GoogleCalendarDescriptor(
        GoogleCalendarId calendarId,
        string displayName,
        bool isPrimary,
        bool isApplicationManaged)
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

        CalendarId = calendarId;
        DisplayName = normalizedDisplayName;
        IsPrimary = isPrimary;
        IsApplicationManaged = isApplicationManaged;
    }
}
