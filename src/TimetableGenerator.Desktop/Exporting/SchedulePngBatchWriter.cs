using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Controls;

using TimetableGenerator.Desktop.Views;

namespace TimetableGenerator.Desktop.Exporting;

internal sealed class SchedulePngBatchWriter
{
    private readonly IControlPngExporter mPngExporter;

    public SchedulePngBatchWriter(IControlPngExporter pngExporter)
    {
        if (pngExporter == null)
        {
            throw new ArgumentNullException(nameof(pngExporter));
        }

        mPngExporter = pngExporter;
    }

    internal async Task exportAsync(
        SchedulePngExportBatch exportBatch,
        SchedulePngBatchDirectory destinationDirectory,
        ScheduleBoardView sizingBoard,
        Canvas exportHost,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exportBatch);
        ArgumentNullException.ThrowIfNull(destinationDirectory);
        ArgumentNullException.ThrowIfNull(sizingBoard);
        ArgumentNullException.ThrowIfNull(exportHost);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        ScheduleBoardPngExportSnapshot snapshot;
        try
        {
            snapshot = ScheduleBoardPngExportSnapshot.create(
                exportHost,
                exportBatch.Candidates[0],
                sizingBoard);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new SchedulePngBatchExportException(
                0,
                exportBatch.Candidates.Count,
                new Exception[] { exception });
        }

        using (snapshot)
        {
            int successfulCount = 0;
            List<Exception> failures = new List<Exception>();
            for (int candidateIndex = 0;
                candidateIndex < exportBatch.Candidates.Count;
                ++candidateIndex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (candidateIndex > 0)
                    {
                        snapshot.update(
                            exportBatch.Candidates[candidateIndex],
                            sizingBoard);
                    }

                    SchedulePngCandidateNumber candidateNumber =
                        new SchedulePngCandidateNumber(
                            candidateIndex + 1,
                            exportBatch.Candidates.Count);
                    string fileName =
                        SchedulePngFileNameFactory.CreateBatchCandidate(
                            exportBatch.PlanName,
                            candidateNumber);
                    using (Stream destinationStream =
                        destinationDirectory.createFile(fileName))
                    {
                        await mPngExporter.ExportControlAsync(
                            snapshot.Surface,
                            destinationStream,
                            cancellationToken);
                        await destinationStream.FlushAsync(
                            cancellationToken);
                    }

                    successfulCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }

            if (failures.Count > 0)
            {
                throw new SchedulePngBatchExportException(
                    successfulCount,
                    failures.Count,
                    failures);
            }
        }
    }
}
