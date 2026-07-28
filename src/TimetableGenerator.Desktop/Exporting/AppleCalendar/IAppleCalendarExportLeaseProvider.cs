using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal interface IAppleCalendarExportLeaseProvider
{
    Task<IAppleCalendarExportLease> AcquireAsync(CancellationToken cancellationToken);
}
