using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal interface IAppleCalendarExporter
{
    bool IsAvailable { get; }

    Task<AppleCalendarExportResult> ExportAsync(CalendarExportDocument document, ICalendarNameConflictResolver conflictResolver, CancellationToken cancellationToken);
}
