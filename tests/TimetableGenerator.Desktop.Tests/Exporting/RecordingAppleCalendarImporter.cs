using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

namespace TimetableGenerator.Desktop.Tests.Exporting;

internal sealed class RecordingAppleCalendarImporter : IAppleCalendarImporter
{
    private readonly EAppleCalendarRuntimePlatform mRuntimePlatform;

    public bool IsAvailable
    {
        get
        {
            return mRuntimePlatform == EAppleCalendarRuntimePlatform.MacOS;
        }
    }

    public IcsCalendarFilePath? OpenedFilePathOrNull { get; private set; }

    public RecordingAppleCalendarImporter(
        EAppleCalendarRuntimePlatform runtimePlatform)
    {
        mRuntimePlatform = runtimePlatform;
    }

    public Task OpenImportAsync(
        IcsCalendarFilePath calendarFilePath,
        CancellationToken cancellationToken)
    {
        if (calendarFilePath == null)
        {
            throw new ArgumentNullException(nameof(calendarFilePath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (IsAvailable == false)
        {
            throw new PlatformNotSupportedException(
                "Apple Calendar import is unavailable on this test platform.");
        }

        OpenedFilePathOrNull = calendarFilePath;
        return Task.CompletedTask;
    }
}
