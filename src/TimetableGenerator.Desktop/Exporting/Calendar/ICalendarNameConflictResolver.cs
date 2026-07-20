using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.Calendar;

internal interface ICalendarNameConflictResolver
{
    Task<ECalendarNameConflictResolution> ResolveAsync(
        CalendarNameConflict conflict,
        CancellationToken cancellationToken);
}
