using System;
using System.IO;

using TimetableGenerator.Desktop.Storage;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarIntegrationFactory
{
    private const string EXPORT_LOCK_FILE_NAME = "apple-calendar-export.lock";
    private const string OWNERSHIP_REGISTRY_FILE_NAME = "apple-calendar-ownership.json";

    public static IAppleCalendarExporter Create(ProductDataRootPath dataRootPath)
    {
        if (dataRootPath == null)
        {
            throw new ArgumentNullException(nameof(dataRootPath));
        }

        string integrationsDirectory = Path.Combine(dataRootPath.Value, "Integrations");
        FileAppleCalendarExportLeaseProvider exportLeaseProvider = new FileAppleCalendarExportLeaseProvider(new AppleCalendarExportLockFilePath(Path.Combine(integrationsDirectory, EXPORT_LOCK_FILE_NAME)));
        FileAppleCalendarOwnershipRegistryStore registryStore = new FileAppleCalendarOwnershipRegistryStore(new AppleCalendarOwnershipRegistryFilePath(Path.Combine(integrationsDirectory, OWNERSHIP_REGISTRY_FILE_NAME)));
        EventKitAppleCalendarNativeBridge nativeBridge = new EventKitAppleCalendarNativeBridge(new NativeEventKitCalendarCommand(), registryStore);
        return new AppleCalendarExportService(nativeBridge, exportLeaseProvider);
    }
}
