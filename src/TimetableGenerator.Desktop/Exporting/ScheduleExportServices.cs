using System;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

namespace TimetableGenerator.Desktop.Exporting;

internal sealed class ScheduleExportServices
{
    public IControlPngExporter PngExporter { get; }

    public IGoogleCalendarExporter GoogleCalendarExporter { get; }

    public IGoogleCalendarWebNavigator GoogleCalendarWebNavigator { get; }

    public IAppleCalendarExporter AppleCalendarExporter { get; }

    public ICalendarTimeZoneProvider CalendarTimeZoneProvider { get; }

    public ScheduleExportServices(
        IControlPngExporter pngExporter,
        IGoogleCalendarExporter googleCalendarExporter,
        IGoogleCalendarWebNavigator googleCalendarWebNavigator,
        IAppleCalendarExporter appleCalendarExporter,
        ICalendarTimeZoneProvider calendarTimeZoneProvider)
    {
        if (pngExporter == null)
        {
            throw new ArgumentNullException(nameof(pngExporter));
        }

        if (googleCalendarExporter == null)
        {
            throw new ArgumentNullException(nameof(googleCalendarExporter));
        }

        if (googleCalendarWebNavigator == null)
        {
            throw new ArgumentNullException(nameof(googleCalendarWebNavigator));
        }

        if (appleCalendarExporter == null)
        {
            throw new ArgumentNullException(nameof(appleCalendarExporter));
        }

        if (calendarTimeZoneProvider == null)
        {
            throw new ArgumentNullException(nameof(calendarTimeZoneProvider));
        }

        PngExporter = pngExporter;
        GoogleCalendarExporter = googleCalendarExporter;
        GoogleCalendarWebNavigator = googleCalendarWebNavigator;
        AppleCalendarExporter = appleCalendarExporter;
        CalendarTimeZoneProvider = calendarTimeZoneProvider;
    }
}
