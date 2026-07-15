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
        IProgress<SchedulePngExportProgress>? progressOrNull,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<ExportedSchedulePng> exportedFiles = new List<ExportedSchedulePng>();
        List<SchedulePngExportFailure> failures = new List<SchedulePngExportFailure>();
        SchedulePngRequestedFileName requestedFileName = buildRequestedFileName(
            request.BaseName,
            request.ScheduleNumber);

        try
        {
            prepareDestinationDirectory(request.DestinationDirectory);
        }
        catch (Exception exception) when (isRecoverableExportException(exception))
        {
            SchedulePngExportFailure failure = createFailure(
                request.ScheduleNumber,
                requestedFileName,
                request.DestinationDirectory,
                exception);
            failures.Add(failure);
            SchedulePngExportProgress exportProgress = createProgress(
                new SchedulePngExportProgressPosition(1, 1),
                request.ScheduleNumber,
                ESchedulePngExportItemStatus.Failed);
            reportProgress(
                progressOrNull,
                exportProgress);
            return new SchedulePngExportResult(exportedFiles, failures);
        }

        try
        {
            ExportedSchedulePng exportedFile = exportSchedule(
                request.ScheduleGrid,
                request.ScheduleNumber,
                request.DestinationDirectory,
                requestedFileName,
                cancellationToken);
            exportedFiles.Add(exportedFile);
        }
        catch (Exception exception) when (isRecoverableExportException(exception))
        {
            SchedulePngExportFailure failure = createFailure(
                request.ScheduleNumber,
                requestedFileName,
                request.DestinationDirectory,
                exception);
            failures.Add(failure);
            SchedulePngExportProgress exportProgress = createProgress(
                new SchedulePngExportProgressPosition(1, 1),
                request.ScheduleNumber,
                ESchedulePngExportItemStatus.Failed);
            reportProgress(
                progressOrNull,
                exportProgress);
            return new SchedulePngExportResult(exportedFiles, failures);
        }

        try
        {
            SchedulePngExportProgress exportProgress = createProgress(
                new SchedulePngExportProgressPosition(1, 1),
                request.ScheduleNumber,
                ESchedulePngExportItemStatus.Succeeded);
            reportProgress(
                progressOrNull,
                exportProgress);
        }
        catch (Exception exception)
        {
            IReadOnlyList<SchedulePngExportArtifact> retainedArtifacts =
                rollbackExportedFiles(exportedFiles);
            if (retainedArtifacts.Count > 0)
            {
                throw new SchedulePngExportCleanupException(
                    retainedArtifacts,
                    exception);
            }

            throw;
        }

        return new SchedulePngExportResult(exportedFiles, failures);
    }

    private SchedulePngExportResult exportAll(
        SchedulePngBatchExportRequest request,
        IProgress<SchedulePngExportProgress>? progressOrNull,
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
            addDestinationFailure(
                request,
                exception,
                failures,
                progressOrNull,
                cancellationToken);
            return new SchedulePngExportResult(exportedFiles, failures);
        }

        try
        {
            for (int scheduleIndex = 0; scheduleIndex < totalScheduleCount; ++scheduleIndex)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ScheduleExportNumber scheduleNumber = new ScheduleExportNumber(scheduleIndex + 1);
                SchedulePngRequestedFileName requestedFileName =
                    buildRequestedFileName(request.BaseName, scheduleNumber);
                ESchedulePngExportItemStatus itemStatus =
                    ESchedulePngExportItemStatus.Failed;

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
                }

                SchedulePngExportProgress exportProgress = createProgress(
                    new SchedulePngExportProgressPosition(
                        scheduleIndex + 1,
                        totalScheduleCount),
                    scheduleNumber,
                    itemStatus);
                reportProgress(
                    progressOrNull,
                    exportProgress);
            }
        }
        catch (SchedulePngExportCleanupException exception)
        {
            List<SchedulePngExportArtifact> retainedArtifacts =
                new List<SchedulePngExportArtifact>(exception.RetainedArtifacts);
            IReadOnlyList<SchedulePngExportArtifact> rollbackArtifacts =
                rollbackExportedFiles(exportedFiles);
            retainedArtifacts.AddRange(rollbackArtifacts);
            throw new SchedulePngExportCleanupException(retainedArtifacts, exception);
        }
        catch (OperationCanceledException exception)
        {
            IReadOnlyList<SchedulePngExportArtifact> retainedArtifacts =
                rollbackExportedFiles(exportedFiles);
            if (retainedArtifacts.Count > 0)
            {
                throw new SchedulePngExportCleanupException(
                    retainedArtifacts,
                    exception);
            }

            return SchedulePngExportResult.createCanceled(
                Array.Empty<SchedulePngExportArtifact>());
        }
        catch (Exception exception)
        {
            IReadOnlyList<SchedulePngExportArtifact> retainedArtifacts =
                rollbackExportedFiles(exportedFiles);
            if (retainedArtifacts.Count > 0)
            {
                throw new SchedulePngExportCleanupException(
                    retainedArtifacts,
                    exception);
            }

            throw;
        }

        return new SchedulePngExportResult(exportedFiles, failures);
    }

    private static void prepareDestinationDirectory(
        ScheduleExportDirectoryPath destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory.Value);
    }

    private void addDestinationFailure(
        SchedulePngBatchExportRequest request,
        Exception exception,
        ICollection<SchedulePngExportFailure> failures,
        IProgress<SchedulePngExportProgress>? progressOrNull,
        CancellationToken cancellationToken)
    {
        int totalScheduleCount = request.ScheduleGrids.Count;
        cancellationToken.ThrowIfCancellationRequested();

        ScheduleExportNumber firstScheduleNumber = new ScheduleExportNumber(1);
        SchedulePngRequestedFileName requestedFileName = buildRequestedFileName(
            request.BaseName,
            firstScheduleNumber);
        SchedulePngExportFailure failure = createFailure(
            firstScheduleNumber,
            requestedFileName,
            request.DestinationDirectory,
            exception);
        failures.Add(failure);
        SchedulePngExportProgress exportProgress = createProgress(
            new SchedulePngExportProgressPosition(1, totalScheduleCount),
            firstScheduleNumber,
            ESchedulePngExportItemStatus.Failed);
        reportProgress(
            progressOrNull,
            exportProgress);
    }

    private ExportedSchedulePng exportSchedule(
        ScheduleGridViewModel scheduleGrid,
        ScheduleExportNumber scheduleNumber,
        ScheduleExportDirectoryPath destinationDirectory,
        SchedulePngRequestedFileName requestedFileName,
        CancellationToken cancellationToken)
    {
        RenderedSchedulePng renderedPng = mRenderer.Render(scheduleGrid, cancellationToken);
        SchedulePngOutputFilePath outputFilePath = writeToUniqueFile(
            renderedPng,
            scheduleNumber,
            destinationDirectory,
            requestedFileName,
            cancellationToken);
        return new ExportedSchedulePng(scheduleNumber, outputFilePath);
    }

    private static SchedulePngOutputFilePath writeToUniqueFile(
        RenderedSchedulePng renderedPng,
        ScheduleExportNumber scheduleNumber,
        ScheduleExportDirectoryPath destinationDirectory,
        SchedulePngRequestedFileName requestedFileName,
        CancellationToken cancellationToken)
    {
        string requestedFileStem = requestedFileName.FileStem;

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
            if (File.Exists(candidateFilePath))
            {
                continue;
            }

            string temporaryFilePath = candidateFilePath
                + "."
                + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)
                + ".tmp";
            FileStream outputStream = new FileStream(
                temporaryFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                FILE_STREAM_BUFFER_SIZE_BYTES,
                FileOptions.SequentialScan);

            bool hasCommittedOutputFile = false;
            try
            {
                using (outputStream)
                {
                    renderedPng.writeTo(outputStream);
                    outputStream.Flush(true);
                }

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(temporaryFilePath, candidateFilePath, false);
                    hasCommittedOutputFile = true;
                }
                catch (IOException exception)
                {
                    if (File.Exists(candidateFilePath))
                    {
                        if (tryDeleteExportArtifact(temporaryFilePath) == false)
                        {
                            SchedulePngExportArtifact retainedTemporaryArtifact =
                                createExportArtifact(
                                    scheduleNumber,
                                    temporaryFilePath,
                                    ESchedulePngExportArtifactKind.TemporaryFile);
                            throw new SchedulePngExportCleanupException(
                                new SchedulePngExportArtifact[] { retainedTemporaryArtifact },
                                exception);
                        }

                        continue;
                    }

                    throw;
                }

                cancellationToken.ThrowIfCancellationRequested();
                return new SchedulePngOutputFilePath(candidateFilePath);
            }
            catch (SchedulePngExportCleanupException)
            {
                throw;
            }
            catch (Exception exception)
            {
                List<SchedulePngExportArtifact> retainedArtifacts =
                    new List<SchedulePngExportArtifact>();
                if (tryDeleteExportArtifact(temporaryFilePath) == false)
                {
                    retainedArtifacts.Add(createExportArtifact(
                        scheduleNumber,
                        temporaryFilePath,
                        ESchedulePngExportArtifactKind.TemporaryFile));
                }

                if (hasCommittedOutputFile)
                {
                    if (tryDeleteExportArtifact(candidateFilePath) == false)
                    {
                        retainedArtifacts.Add(createExportArtifact(
                            scheduleNumber,
                            candidateFilePath,
                            ESchedulePngExportArtifactKind.CompletedPng));
                    }
                }

                if (retainedArtifacts.Count > 0)
                {
                    throw new SchedulePngExportCleanupException(
                        retainedArtifacts,
                        exception);
                }

                throw;
            }
        }

        throw new IOException(
            "A unique PNG file name could not be allocated after "
            + MAXIMUM_UNIQUE_FILE_NAME_ATTEMPTS.ToString(CultureInfo.InvariantCulture)
            + " attempts.");
    }

    private static bool tryDeleteExportArtifact(string filePath)
    {
        try
        {
            File.Delete(filePath);
            return true;
        }
        catch (Exception exception) when (isRecoverableExportException(exception))
        {
            return false;
        }
    }

    private static IReadOnlyList<SchedulePngExportArtifact> rollbackExportedFiles(
        IEnumerable<ExportedSchedulePng> exportedFiles)
    {
        List<SchedulePngExportArtifact> retainedArtifacts =
            new List<SchedulePngExportArtifact>();
        foreach (ExportedSchedulePng exportedFile in exportedFiles)
        {
            if (tryDeleteExportArtifact(exportedFile.OutputFilePath.Value) == false)
            {
                retainedArtifacts.Add(createExportArtifact(
                    exportedFile.ScheduleNumber,
                    exportedFile.OutputFilePath.Value,
                    ESchedulePngExportArtifactKind.CompletedPng));
            }
        }

        return retainedArtifacts.AsReadOnly();
    }

    private static SchedulePngExportArtifact createExportArtifact(
        ScheduleExportNumber scheduleNumber,
        string filePath,
        ESchedulePngExportArtifactKind kind)
    {
        SchedulePngExportArtifactFilePath artifactFilePath =
            new SchedulePngExportArtifactFilePath(filePath);
        return new SchedulePngExportArtifact(
            scheduleNumber,
            artifactFilePath,
            kind);
    }

    private static SchedulePngRequestedFileName buildRequestedFileName(
        ScheduleExportBaseName baseName,
        ScheduleExportNumber scheduleNumber)
    {
        string requestedFileName = baseName.Value
            + "_시간표_"
            + scheduleNumber.Value.ToString("D2", CultureInfo.InvariantCulture)
            + ".png";
        return new SchedulePngRequestedFileName(requestedFileName);
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
        SchedulePngRequestedFileName requestedFileName,
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
            requestedFileName.Value);
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
        IProgress<SchedulePngExportProgress>? progressOrNull,
        SchedulePngExportProgress progress)
    {
        if (progressOrNull == null)
        {
            return;
        }

        progressOrNull.Report(progress);
    }

    private static SchedulePngExportProgress createProgress(
        SchedulePngExportProgressPosition position,
        ScheduleExportNumber scheduleNumber,
        ESchedulePngExportItemStatus itemStatus)
    {
        SchedulePngExportProgress progress = new SchedulePngExportProgress(
            position,
            scheduleNumber,
            itemStatus);
        return progress;
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
