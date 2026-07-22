using System;
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
        using (ScheduleBoardPngExportSnapshot snapshot =
            ScheduleBoardPngExportSnapshot.create(
                exportHost,
                exportBatch.Candidates[0],
                sizingBoard))
        {
            for (int candidateIndex = 0;
                candidateIndex < exportBatch.Candidates.Count;
                ++candidateIndex)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidateIndex > 0)
                {
                    snapshot.update(exportBatch.Candidates[candidateIndex], sizingBoard);
                }

                SchedulePngCandidateNumber candidateNumber =
                    new SchedulePngCandidateNumber(
                        candidateIndex + 1,
                        exportBatch.Candidates.Count);
                string fileName =
                    SchedulePngFileNameFactory.CreateBatchCandidate(
                        exportBatch.PlanName,
                        candidateNumber);
                using (Stream destinationStream = destinationDirectory.createFile(fileName))
                {
                    await mPngExporter.ExportControlAsync(snapshot.Surface, destinationStream, cancellationToken);
                    await destinationStream.FlushAsync(cancellationToken);
                }
            }
        }
    }
}
