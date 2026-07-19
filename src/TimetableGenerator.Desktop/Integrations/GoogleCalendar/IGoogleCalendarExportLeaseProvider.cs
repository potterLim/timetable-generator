using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal interface IGoogleCalendarExportLeaseProvider
{
    Task<IGoogleCalendarExportLease> AcquireAsync(CancellationToken cancellationToken);
}
