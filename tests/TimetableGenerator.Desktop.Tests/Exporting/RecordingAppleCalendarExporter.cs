using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Tests.Exporting;

internal sealed class RecordingAppleCalendarExporter : IAppleCalendarExporter
{
    private readonly AppleCalendarExportResult mResult;

    public bool IsAvailable { get; }

    public CalendarExportDocument? ExportedDocumentOrNull { get; private set; }

    public RecordingAppleCalendarExporter(bool isAvailable, AppleCalendarExportResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        IsAvailable = isAvailable;
        mResult = result;
    }

    public Task<AppleCalendarExportResult> ExportAsync(
        CalendarExportDocument document,
        ICalendarNameConflictResolver conflictResolver,
        CancellationToken cancellationToken)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (conflictResolver == null)
        {
            throw new ArgumentNullException(nameof(conflictResolver));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ExportedDocumentOrNull = document;
        return Task.FromResult(mResult);
    }
}
