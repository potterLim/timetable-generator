using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal sealed class IcsCalendarFileStore
{
    private const int WRITE_BUFFER_SIZE_BYTES = 8_192;

    private static readonly UTF8Encoding UTF8_WITHOUT_BYTE_ORDER_MARK =
        new UTF8Encoding(false, true);

    private readonly CalendarExportDirectoryPath mDirectoryPath;

    public IcsCalendarFileStore(CalendarExportDirectoryPath directoryPath)
    {
        if (directoryPath == null)
        {
            throw new ArgumentNullException(nameof(directoryPath));
        }

        mDirectoryPath = directoryPath;
    }

    public async Task<IcsCalendarFilePath> SaveAsync(
        CalendarExportDocument document,
        CalendarExportTimestamp exportTimestamp,
        CancellationToken cancellationToken)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        cancellationToken.ThrowIfCancellationRequested();
        string destinationPath = Path.Combine(
            mDirectoryPath.Value,
            document.PlanId.Value.ToString("N") + ".ics");
        string temporaryPath = destinationPath
            + "."
            + Guid.NewGuid().ToString("N")
            + ".tmp";

        try
        {
            Directory.CreateDirectory(mDirectoryPath.Value);
            string serializedCalendar = IcsCalendarSerializer.Serialize(
                document,
                exportTimestamp);
            byte[] content = UTF8_WITHOUT_BYTE_ORDER_MARK.GetBytes(
                serializedCalendar);
            using (FileStream outputStream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                WRITE_BUFFER_SIZE_BYTES,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await outputStream.WriteAsync(
                    content,
                    cancellationToken);
                await outputStream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, true);
            return new IcsCalendarFilePath(destinationPath);
        }
        catch (Exception exception) when (canWrapSaveFailure(exception))
        {
            throw new CalendarExportPersistenceException(
                "The calendar import file could not be saved.",
                exception);
        }
        finally
        {
            tryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static bool canWrapSaveFailure(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is EncoderFallbackException;
    }

    private static void tryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException)
        {
            Trace.TraceWarning(
                "A temporary calendar export file could not be removed: {0}",
                exception);
        }
    }
}
