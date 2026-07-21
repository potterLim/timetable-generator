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
        return new ScheduleExportServices(
            new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY),
            GoogleCalendarIntegrationFactory.Create(dataRootPath),
            new DefaultGoogleCalendarWebNavigator(),
            new AppleCalendarExportService(
                new JxaAppleCalendarNativeBridge()),
            new SystemCalendarTimeZoneProvider());
    }
}
