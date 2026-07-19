using System;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal interface IGoogleCalendarExporter : IDisposable
{
    Task<GoogleCalendarExportResult> ExportAsync(
        GoogleCalendarExportPlan plan,
        CancellationToken cancellationToken);
}
