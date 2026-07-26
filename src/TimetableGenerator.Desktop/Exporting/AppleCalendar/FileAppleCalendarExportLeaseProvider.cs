using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class FileAppleCalendarExportLeaseProvider
    : IAppleCalendarExportLeaseProvider
{
    private const int WINDOWS_ERROR_SHARING_VIOLATION = 32;
    private const int WINDOWS_ERROR_LOCK_VIOLATION = 33;

    private static readonly TimeSpan RETRY_DELAY =
        TimeSpan.FromMilliseconds(100.0);

    private static readonly TimeSpan MAXIMUM_WAIT =
        TimeSpan.FromSeconds(30.0);

    private readonly AppleCalendarExportLockFilePath mPath;

    public FileAppleCalendarExportLeaseProvider(
        AppleCalendarExportLockFilePath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        mPath = path;
    }

    public async Task<IAppleCalendarExportLease> AcquireAsync(
        CancellationToken cancellationToken)
    {
        string? directoryPathOrNull = Path.GetDirectoryName(mPath.Value);
        if (directoryPathOrNull == null)
        {
            throw new InvalidOperationException(
                "The Apple Calendar export lock path does not contain a directory.");
        }

        Directory.CreateDirectory(directoryPathOrNull);
        Stopwatch waitTimer = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                FileStream stream = new FileStream(
                    mPath.Value,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.None);
                return new FileAppleCalendarExportLease(stream);
            }
            catch (IOException exception) when (isLockContention(exception))
            {
                if (waitTimer.Elapsed >= MAXIMUM_WAIT)
                {
                    throw new IOException(
                        "Another process did not release the Apple Calendar export lock.",
                        exception);
                }

                await Task.Delay(RETRY_DELAY, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private static bool isLockContention(IOException exception)
    {
        if (OperatingSystem.IsWindows())
        {
            int nativeErrorCode = exception.HResult & 0xFFFF;
            return nativeErrorCode == WINDOWS_ERROR_SHARING_VIOLATION
                || nativeErrorCode == WINDOWS_ERROR_LOCK_VIOLATION;
        }

        return OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();
    }

    private sealed class FileAppleCalendarExportLease
        : IAppleCalendarExportLease
    {
        private readonly FileStream mStream;

        public FileAppleCalendarExportLease(FileStream stream)
        {
            mStream = stream;
        }

        public ValueTask DisposeAsync()
        {
            return mStream.DisposeAsync();
        }
    }
}
