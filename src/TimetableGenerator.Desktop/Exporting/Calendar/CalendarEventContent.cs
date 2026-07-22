using System;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class CalendarEventContent
{
    public string Summary { get; }

    public string Location { get; }

    public string Description { get; }

    public bool HasLocation
    {
        get
        {
            return Location.Length > 0;
        }
    }

    public bool HasDescription
    {
        get
        {
            return Description.Length > 0;
        }
    }

    public CalendarEventContent(string summary, string location, string description)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Calendar events require a summary.", nameof(summary));
        }

        if (location == null)
        {
            throw new ArgumentNullException(nameof(location));
        }

        if (description == null)
        {
            throw new ArgumentNullException(nameof(description));
        }

        Summary = summary.Trim();
        Location = location.Trim();
        Description = description.Trim();
    }
}
