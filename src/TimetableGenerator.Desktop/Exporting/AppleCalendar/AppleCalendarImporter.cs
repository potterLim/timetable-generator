using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarImporter : IAppleCalendarImporter
{
    private readonly EAppleCalendarRuntimePlatform mRuntimePlatform;
    private readonly IAppleCalendarOpenCommand mOpenCommand;

    public bool IsAvailable
    {
        get
        {
            return mRuntimePlatform == EAppleCalendarRuntimePlatform.MacOS;
        }
    }

    public AppleCalendarImporter()
        : this(
            AppleCalendarRuntimePlatformPolicy.FindCurrentPlatform(),
            new ProcessAppleCalendarOpenCommand())
    {
    }

    internal AppleCalendarImporter(
        EAppleCalendarRuntimePlatform runtimePlatform,
        IAppleCalendarOpenCommand openCommand)
    {
        ArgumentNullException.ThrowIfNull(openCommand);

        mRuntimePlatform = runtimePlatform;
        mOpenCommand = openCommand;
    }

    public Task OpenImportAsync(
        IcsCalendarFilePath calendarFilePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calendarFilePath);

        cancellationToken.ThrowIfCancellationRequested();

        if (IsAvailable == false)
        {
            throw new PlatformNotSupportedException(
                "Apple Calendar import is available only on macOS.");
        }

        if (File.Exists(calendarFilePath.Value) == false)
        {
            throw new FileNotFoundException(
                "The iCalendar file to open does not exist.",
                calendarFilePath.Value);
        }

        return mOpenCommand.RunAsync(calendarFilePath, cancellationToken);
    }
}
