using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

namespace TimetableGenerator.Desktop.Tests.Exporting;

internal sealed class RecordingGoogleCalendarExporter : IGoogleCalendarExporter
{
    private readonly GoogleCalendarExportResult mResult;

    public GoogleCalendarExportPlan? ExportedPlanOrNull { get; private set; }

    public bool IsDisposed { get; private set; }

    public RecordingGoogleCalendarExporter(GoogleCalendarExportResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        mResult = result;
    }

    public Task<GoogleCalendarExportResult> ExportAsync(
        GoogleCalendarExportPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ExportedPlanOrNull = plan;
        return Task.FromResult(mResult);
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
