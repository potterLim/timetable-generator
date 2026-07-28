using System;
using System.IO;

using TimetableGenerator.Desktop.Storage;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarIntegrationFactory
{
    private const string EXPORT_LOCK_FILE_NAME = "apple-calendar-export.lock";

    public static IAppleCalendarExporter Create(ProductDataRootPath dataRootPath)
    {
        if (dataRootPath == null)
        {
            throw new ArgumentNullException(nameof(dataRootPath));
        }

        FileAppleCalendarExportLeaseProvider exportLeaseProvider = new FileAppleCalendarExportLeaseProvider(new AppleCalendarExportLockFilePath(Path.Combine(dataRootPath.Value, "Integrations", EXPORT_LOCK_FILE_NAME)));
        return new AppleCalendarExportService(new JxaAppleCalendarNativeBridge(), exportLeaseProvider);
    }
}
