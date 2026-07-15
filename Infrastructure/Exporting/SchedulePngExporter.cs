using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngExporter
{
    private const int MAXIMUM_UNIQUE_FILE_NAME_ATTEMPTS = 10_000;
    private const int FILE_STREAM_BUFFER_SIZE_BYTES = 81_920;

    private readonly SchedulePngRenderer mRenderer;

    public SchedulePngExporter()
        : this(new SchedulePngRenderer())
    {
    }

    public SchedulePngExporter(SchedulePngRenderer renderer)
    {
        if (renderer == null)
        {
            throw new ArgumentNullException(nameof(renderer));
        }

        mRenderer = renderer;
    }

    public Task<SchedulePngExportResult> ExportCurrentAsync(
        SchedulePngExportRequest request,
        CancellationToken cancellationToken)
    {
        validateRequest(request);
        return Task.Run(
            () => exportCurrent(request, null, cancellationToken),
            cancellationToken);
    }

    public Task<SchedulePngExportResult> ExportCurrentAsync(
        SchedulePngExportRequest request,
        IProgress<SchedulePngExportProgress> progress,
        CancellationToken cancellationToken)
    {
        validateRequest(request);
        if (progress == null)
        {
            throw new ArgumentNullException(nameof(progress));
        }

        return Task.Run(
            () => exportCurrent(request, progress, cancellationToken),
            cancellationToken);
    }

    public Task<SchedulePngExportResult> ExportAllAsync(
        SchedulePngBatchExportRequest request,
        CancellationToken cancellationToken)
    {
        validateRequest(request);
        return Task.Run(
            () => exportAll(request, null, cancellationToken),
            cancellationToken);
    }

    public Task<SchedulePngExportResult> ExportAllAsync(
        SchedulePngBatchExportRequest request,
        IProgress<SchedulePngExportProgress> progress,
        CancellationToken cancellationToken)
    {
        validateRequest(request);
        if (progress == null)
        {
            throw new ArgumentNullException(nameof(progress));
        }

        return Task.Run(
            () => exportAll(request, progress, cancellationToken),
            cancellationToken);
    }

    private static void validateRequest(SchedulePngExportRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
    }

    private static void validateRequest(SchedulePngBatchExportRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }
    }

    private SchedulePngExportResult exportCurrent(
        SchedulePngExportRequest request,
        IProgress<SchedulePngExportProgress> progressOrNull,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<ExportedSchedulePng> exportedFiles = new List<ExportedSchedulePng>();
        List<SchedulePngExportFailure> failures = new List<SchedulePngExportFailure>();
        string requestedFileName = buildRequestedFileName(request.BaseName, request.ScheduleNumber);

        try
        {
            prepareDestinationDirectory(request.DestinationDirectory);
            ExportedSchedulePng exportedFile = exportSchedule(
                request.ScheduleGrid,
                request.ScheduleNumber,
                request.DestinationDirectory,
                requestedFileName,
                cancellationToken);
            exportedFiles.Add(exportedFile);
            reportProgress(
                progressOrNull,
                1,
                1,
                request.ScheduleNumber,
                ESchedulePngExportItemStatus.Succeeded);
        }
        catch (Exception exception) when (isRecoverableExportException(exception))
        {
            SchedulePngExportFailure failure = createFailure(
                request.ScheduleNumber,
                requestedFileName,
                request.DestinationDirectory,
                exception);
            failures.Add(failure);
            reportProgress(
                progressOrNull,
                1,
                1,
                request.ScheduleNumber,
                ESchedulePngExportItemStatus.Failed);
        }

        return new SchedulePngExportResult(exportedFiles, failures);
    }

    private SchedulePngExportResult exportAll(
        SchedulePngBatchExportRequest request,
        IProgress<SchedulePngExportProgress> progressOrNull,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<ExportedSchedulePng> exportedFiles = new List<ExportedSchedulePng>();
        List<SchedulePngExportFailure> failures = new List<SchedulePngExportFailure>();
        int totalScheduleCount = request.ScheduleGrids.Count;

        try
        {
            prepareDestinationDirectory(request.DestinationDirectory);
        }
        catch (Exception exception) when (isRecoverableExportException(exception))
        {
            addDestinationFailures(
                request,
                exception,
                failures,
                progressOrNull,
                cancellationToken);
            return new SchedulePngExportResult(exportedFiles, failures);
        }

        for (int scheduleIndex = 0; scheduleIndex < totalScheduleCount; ++scheduleIndex)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ScheduleExportNumber scheduleNumber = new ScheduleExportNumber(scheduleIndex + 1);
            string requestedFileName = buildRequestedFileName(request.BaseName, scheduleNumber);
            ESchedulePngExportItemStatus itemStatus;

            try
            {
                ExportedSchedulePng exportedFile = exportSchedule(
                    request.ScheduleGrids[scheduleIndex],
                    scheduleNumber,
                    request.DestinationDirectory,
                    requestedFileName,
                    cancellationToken);
                exportedFiles.Add(exportedFile);
                itemStatus = ESchedulePngExportItemStatus.Succeeded;
            }
            catch (Exception exception) when (isRecoverableExportException(exception))
            {
                SchedulePngExportFailure failure = createFailure(
                    scheduleNumber,
                    requestedFileName,
                    request.DestinationDirectory,
                    exception);
                failures.Add(failure);
                itemStatus = ESchedulePngExportItemStatus.Failed;
            }

            reportProgress(
                progressOrNull,
                scheduleIndex + 1,
                totalScheduleCount,
                scheduleNumber,
                itemStatus);
        }

        return new SchedulePngExportResult(exportedFiles, failures);
    }

    private static void prepareDestinationDirectory(
        ScheduleExportDirectoryPath destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory.Value);
    }

    private void addDestinationFailures(
        SchedulePngBatchExportRequest request,
        Exception exception,
        ICollection<SchedulePngExportFailure> failures,
        IProgress<SchedulePngExportProgress> progressOrNull,
        CancellationToken cancellationToken)
    {
        int totalScheduleCount = request.ScheduleGrids.Count;
        for (int scheduleIndex = 0; scheduleIndex < totalScheduleCount; ++scheduleIndex)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ScheduleExportNumber scheduleNumber = new ScheduleExportNumber(scheduleIndex + 1);
            string requestedFileName = buildRequestedFileName(request.BaseName, scheduleNumber);
            SchedulePngExportFailure failure = createFailure(
                scheduleNumber,
                requestedFileName,
                request.DestinationDirectory,
                exception);
            failures.Add(failure);
            reportProgress(
                progressOrNull,
                scheduleIndex + 1,
                totalScheduleCount,
                scheduleNumber,
                ESchedulePngExportItemStatus.Failed);
        }
    }

    private ExportedSchedulePng exportSchedule(
        ScheduleGridViewModel scheduleGrid,
        ScheduleExportNumber scheduleNumber,
        ScheduleExportDirectoryPath destinationDirectory,
        string requestedFileName,
        CancellationToken cancellationToken)
    {
        RenderedSchedulePng renderedPng = mRenderer.Render(scheduleGrid, cancellationToken);
        SchedulePngOutputFilePath outputFilePath = writeToUniqueFile(
            renderedPng,
            destinationDirectory,
            requestedFileName,
            cancellationToken);
        return new ExportedSchedulePng(scheduleNumber, outputFilePath);
    }

    private static SchedulePngOutputFilePath writeToUniqueFile(
        RenderedSchedulePng renderedPng,
        ScheduleExportDirectoryPath destinationDirectory,
        string requestedFileName,
        CancellationToken cancellationToken)
    {
        string requestedFileStem = Path.GetFileNameWithoutExtension(requestedFileName);

        for (int attemptNumber = 1;
            attemptNumber <= MAXIMUM_UNIQUE_FILE_NAME_ATTEMPTS;
            ++attemptNumber)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string candidateFileName = buildUniqueCandidateFileName(
                requestedFileStem,
                attemptNumber);
            string candidateFilePath = Path.Combine(
                destinationDirectory.Value,
                candidateFileName);
            FileStream outputStream;

            try
            {
                outputStream = new FileStream(
                    candidateFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    FILE_STREAM_BUFFER_SIZE_BYTES,
                    FileOptions.SequentialScan);
            }
            catch (IOException)
            {
                if (File.Exists(candidateFilePath))
                {
                    continue;
                }

                throw;
            }

            try
            {
                using (outputStream)
                {
                    renderedPng.writeTo(outputStream);
                    outputStream.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return new SchedulePngOutputFilePath(candidateFilePath);
            }
            catch
            {
                deleteIncompleteFile(candidateFilePath);
                throw;
            }
        }

        throw new IOException(
            "A unique PNG file name could not be allocated after "
            + MAXIMUM_UNIQUE_FILE_NAME_ATTEMPTS.ToString(CultureInfo.InvariantCulture)
            + " attempts.");
    }

    private static void deleteIncompleteFile(string filePath)
    {
        try
        {
            File.Delete(filePath);
        }
        catch
        {
            // The original export failure is more actionable than cleanup failure here.
        }
    }

    private static string buildRequestedFileName(
        ScheduleExportBaseName baseName,
        ScheduleExportNumber scheduleNumber)
    {
        return baseName.Value
            + "_시간표_"
            + scheduleNumber.Value.ToString("D2", CultureInfo.InvariantCulture)
            + ".png";
    }

    private static string buildUniqueCandidateFileName(
        string requestedFileStem,
        int attemptNumber)
    {
        if (attemptNumber == 1)
        {
            return requestedFileStem + ".png";
        }

        return requestedFileStem
            + " ("
            + attemptNumber.ToString(CultureInfo.InvariantCulture)
            + ").png";
    }

    private static SchedulePngExportFailure createFailure(
        ScheduleExportNumber scheduleNumber,
        string requestedFileName,
        ScheduleExportDirectoryPath destinationDirectory,
        Exception exception)
    {
        string failureReason = exception.Message.Trim();
        if (failureReason.Length == 0)
        {
            failureReason = "알 수 없는 파일 시스템 오류가 발생했습니다.";
        }

        string destinationFilePath = Path.Combine(
            destinationDirectory.Value,
            requestedFileName);
        string failureMessage = "시간표 "
            + scheduleNumber.Value.ToString(CultureInfo.InvariantCulture)
            + "번 PNG를 저장하지 못했습니다.\n대상: "
            + destinationFilePath
            + "\n원인: "
            + failureReason;
        return new SchedulePngExportFailure(
            scheduleNumber,
            requestedFileName,
            failureMessage);
    }

    private static void reportProgress(
        IProgress<SchedulePngExportProgress> progressOrNull,
        int processedScheduleCount,
        int totalScheduleCount,
        ScheduleExportNumber scheduleNumber,
        ESchedulePngExportItemStatus itemStatus)
    {
        if (progressOrNull == null)
        {
            return;
        }

        SchedulePngExportProgress progress = new SchedulePngExportProgress(
            processedScheduleCount,
            totalScheduleCount,
            scheduleNumber,
            itemStatus);
        progressOrNull.Report(progress);
    }

    private static bool isRecoverableExportException(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is ExternalException
            || exception is ArgumentException
            || exception is InvalidOperationException
            || exception is NotSupportedException
            || exception is SecurityException
            || exception is Win32Exception;
    }
}
