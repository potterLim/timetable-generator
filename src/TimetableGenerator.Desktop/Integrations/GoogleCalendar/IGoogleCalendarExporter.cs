using System;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal interface IGoogleCalendarExporter : IDisposable
{
    Task<GoogleCalendarExportResult> ExportAsync(GoogleCalendarExportPlan plan, ICalendarNameConflictResolver conflictResolver, CancellationToken cancellationToken);
}
