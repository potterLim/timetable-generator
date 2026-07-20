using System;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

namespace TimetableGenerator.Desktop.Exporting;

internal sealed class ScheduleExportServices
{
    public IControlPngExporter PngExporter { get; }

    public IGoogleCalendarExporter GoogleCalendarExporter { get; }

    public IAppleCalendarImporter AppleCalendarImporter { get; }

    public IcsCalendarFileStore IcsFileStore { get; }

    public ICalendarExportClock Clock { get; }

    public ICalendarTimeZoneProvider CalendarTimeZoneProvider { get; }

    public ScheduleExportServices(
        IControlPngExporter pngExporter,
        IGoogleCalendarExporter googleCalendarExporter,
        IAppleCalendarImporter appleCalendarImporter,
        IcsCalendarFileStore icsFileStore,
        ICalendarExportClock clock,
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

        if (appleCalendarImporter == null)
        {
            throw new ArgumentNullException(nameof(appleCalendarImporter));
        }

        if (icsFileStore == null)
        {
            throw new ArgumentNullException(nameof(icsFileStore));
        }

        if (clock == null)
        {
            throw new ArgumentNullException(nameof(clock));
        }

        if (calendarTimeZoneProvider == null)
        {
            throw new ArgumentNullException(nameof(calendarTimeZoneProvider));
        }

        PngExporter = pngExporter;
        GoogleCalendarExporter = googleCalendarExporter;
        AppleCalendarImporter = appleCalendarImporter;
        IcsFileStore = icsFileStore;
        Clock = clock;
        CalendarTimeZoneProvider = calendarTimeZoneProvider;
    }
}
