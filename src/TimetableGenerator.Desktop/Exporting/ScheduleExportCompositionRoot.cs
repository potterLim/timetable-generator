using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Desktop.Storage;

namespace TimetableGenerator.Desktop.Exporting;

internal static class ScheduleExportCompositionRoot
{
    public static ScheduleExportServices CreateDefault()
    {
        ProductDataRootPath dataRootPath = ProductDataRootPath.CreateDefault();
        ProductDataPaths dataPaths = new ProductDataPaths(dataRootPath);
        return new ScheduleExportServices(
            new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY),
            GoogleCalendarIntegrationFactory.Create(dataRootPath),
            new AppleCalendarImporter(),
            new IcsCalendarFileStore(dataPaths.CalendarExports),
            new SystemCalendarExportClock(),
            new SystemCalendarTimeZoneProvider());
    }
}
